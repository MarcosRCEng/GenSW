param(
    [Parameter(Mandatory = $true)][string]$ConfigPath,
    [switch]$Scheduled,
    [switch]$Force
)
$ErrorActionPreference = 'Stop'
$phase = 'configuration'
$privateRoot = $null
$backupId = 'gensw-' + [DateTime]::UtcNow.ToString('yyyyMMddTHHmmssZ') + '-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)

function Assert-PrivatePath([string]$Path, [bool]$Directory) {
    if (-not (Test-Path -LiteralPath $Path)) { throw 'private_path_unavailable' }
    $item = Get-Item -LiteralPath $Path -Force
    if ([bool]$item.PSIsContainer -ne $Directory -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'private_path_invalid'
    }
    $acl = Get-Acl -LiteralPath $Path
    if (-not $acl.AreAccessRulesProtected) { throw 'private_acl_inheritance_enabled' }
    $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if ($rule.AccessControlType -eq 'Allow' -and $sid -notin $allowed) { throw 'private_acl_unapproved_principal' }
    }
}

function Remove-OwnedStage([string]$Path, [string]$Root, [string]$ExpectedName) {
    if (-not (Test-Path -LiteralPath $Path)) { return }
    $resolvedRoot = (Resolve-Path -LiteralPath $Root).Path.TrimEnd('\')
    $resolved = (Resolve-Path -LiteralPath $Path).Path
    if ([IO.Path]::GetDirectoryName($resolved) -ne $resolvedRoot -or [IO.Path]::GetFileName($resolved) -ne $ExpectedName) {
        throw 'cleanup_scope_mismatch'
    }
    if ((Get-Item -LiteralPath $resolved -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'cleanup_reparse_point' }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}

function Invoke-PrivateProcess([string]$Executable, [string[]]$Arguments, [string]$InputFile = '') {
    $psi = [Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $Executable
    $psi.UseShellExecute = $false
    $psi.RedirectStandardInput = $true
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    foreach ($argument in $Arguments) { $psi.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $psi
    try {
        if (-not $process.Start()) { throw 'process_start_failed' }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        if ($InputFile) {
            $inputStream = [IO.File]::OpenRead($InputFile)
            try { $inputStream.CopyTo($process.StandardInput.BaseStream) } finally { $inputStream.Dispose() }
        }
        $process.StandardInput.Close()
        $process.WaitForExit()
        $output = $stdout.GetAwaiter().GetResult()
        $errorOutput = $stderr.GetAwaiter().GetResult()
        if ($privateRoot) {
            [IO.File]::WriteAllText((Join-Path $privateRoot 'logs\last-process-private.log'), $output + [Environment]::NewLine + $errorOutput)
        }
        if ($process.ExitCode -ne 0) { throw 'private_process_failed' }
        return $output
    } finally { $process.Dispose() }
}

function Invoke-RemoteJson([string]$Script, [string[]]$Arguments, [string]$InputFile = '') {
    if ($Script -notmatch '^\.local/share/gensw-[A-Za-z0-9_-]+/[A-Za-z0-9._/-]+$' -or $Script.Split('/') -contains '..') {
        throw 'remote_entry_invalid'
    }
    foreach ($argument in $Arguments) {
        if ($argument -notmatch '^[A-Za-z0-9_.:-]+$') { throw 'remote_argument_invalid' }
    }
    $sshArgs = @('-o', 'BatchMode=yes', '-o', 'StrictHostKeyChecking=yes', '-o', 'ConnectTimeout=20',
        '-o', ('HostName=' + $crypto.SshHostName), $crypto.SshAlias, 'python3', $Script) + $Arguments
    $response = Invoke-PrivateProcess $config.Tools.SshExe $sshArgs $InputFile
    return ($response | ConvertFrom-Json)
}

function Invoke-CaptureProcess([string[]]$Arguments) {
    # File redirection avoids waiting for EOF on a pipe inherited by the restarted API.
    # Wait only for the maintenance PowerShell process, never its long-lived API child.
    foreach ($argument in $Arguments) {
        if ($argument.Contains('"') -or $argument.Contains("`r") -or $argument.Contains("`n") -or $argument.EndsWith('\')) {
            throw 'capture_argument_invalid'
        }
    }
    $stdoutPath = Join-Path $config.LogRoot ($backupId + '-capture-stdout-private.log')
    $stderrPath = Join-Path $config.LogRoot ($backupId + '-capture-stderr-private.log')
    $quotedArguments = ($Arguments | ForEach-Object { '"' + $_ + '"' }) -join ' '
    $process = Start-Process -FilePath $config.Tools.PowerShellExe -ArgumentList $quotedArguments -WindowStyle Hidden `
        -RedirectStandardOutput $stdoutPath -RedirectStandardError $stderrPath -PassThru
    try {
        $process.WaitForExit()
        $exitCode = $process.ExitCode
        # Get-Content permits the writer handle inherited by the detached API.
        $output = Get-Content -LiteralPath $stdoutPath -Raw
        if ($exitCode -ne 0) { throw 'capture_process_failed' }
        return $output
    } finally { $process.Dispose() }
}

try {
    if ($PSVersionTable.PSVersion.Major -lt 7) { throw 'powershell7_required' }
    Assert-PrivatePath $ConfigPath $false
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if (-not $config.Enabled -or $config.Environment -ne 'Homologation' -or $config.SourceAlias -ne 'PILOT-472-CLEAN' -or
        $config.DestinationAlias -ne 'DEVOPS-GENSW-MAIN') { throw 'homologation_configuration_required' }
    if ($Scheduled -and -not $config.Schedule.Enabled) { throw 'schedule_not_enabled' }
    if ($config.TaskContext.Scope -ne 'ProcessOnly' -or
        -not (Test-Path -LiteralPath $config.TaskContext.LocalAppData -PathType Container) -or
        $config.PrivateRoot -ne (Join-Path $config.TaskContext.LocalAppData 'GenSW\pilot-475')) {
        throw 'physical_task_context_required'
    }
    # Only this process and its children use the physical package cache paths.
    # Task Scheduler runs outside the packaged app's virtual filesystem view.
    $env:LOCALAPPDATA = $config.TaskContext.LocalAppData
    foreach ($entry in @($config.TaskContext.PathEntries)) {
        if (-not (Test-Path -LiteralPath $entry -PathType Container) -or $entry.Contains(';')) {
            throw 'task_tool_path_unavailable'
        }
    }
    $env:PATH = (@($config.TaskContext.PathEntries) -join ';') + ';' + $env:PATH
    if ([TimeZoneInfo]::Local.Id -ne 'E. South America Standard Time') { throw 'timezone_mismatch' }
    foreach ($path in @($config.PrivateRoot, $config.StagingRoot, $config.LogRoot)) { Assert-PrivatePath $path $true }
    foreach ($path in @($config.SourceConfig, $config.CryptoConfig, $config.CaptureMaintenance)) { Assert-PrivatePath $path $false }
    $privateRoot = (Resolve-Path -LiteralPath $config.PrivateRoot).Path
    $crypto = Get-Content -LiteralPath $config.CryptoConfig -Raw | ConvertFrom-Json
    if ($crypto.SshAlias -ne 'devops-lab' -or $crypto.SshHostName -ne 'devops-lab.tailaf9418.ts.net') { throw 'destination_not_approved' }
    foreach ($path in @($crypto.RecipientsFile, $crypto.IdentityFile)) { Assert-PrivatePath $path $false }
    foreach ($path in @($config.Tools.PowerShellExe, $config.Tools.SshExe, $config.Tools.TarExe, $crypto.AgeExe)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'required_tool_unavailable' }
    }
    $lockPath = Join-Path $privateRoot '.daily-backup.lock'
    if ((Test-Path -LiteralPath $lockPath) -and ((Get-Item -LiteralPath $lockPath -Force).Attributes -band [IO.FileAttributes]::ReparsePoint)) {
        throw 'lock_reparse_point'
    }
    $lock = [IO.File]::Open($lockPath, [IO.FileMode]::OpenOrCreate, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
    try {
        $localDate = [TimeZoneInfo]::ConvertTime([DateTimeOffset]::UtcNow, [TimeZoneInfo]::Local).ToString('yyyy-MM-dd')
        $lastSuccessPath = Join-Path $privateRoot 'last-success.json'
        if ($Scheduled -and -not $Force -and (Test-Path -LiteralPath $lastSuccessPath)) {
            $last = Get-Content -LiteralPath $lastSuccessPath -Raw | ConvertFrom-Json
            if ($last.localDate -eq $localDate -and $last.executionMode -eq 'Scheduled') {
                @{task=475;ok=$true;skipped=$true;reason='scheduled_backup_already_succeeded_today';backupId=$last.backupId} | ConvertTo-Json -Compress
                exit 0
            }
        }
        $phase = 'destination_preflight'
        $destination = Invoke-RemoteJson $crypto.RemoteReceiver @('--status')
        if ($destination.destination -ne 'DEVOPS-GENSW-MAIN' -or $destination.mode -ne '0700') {
            throw 'destination_preflight_failed'
        }
        $phase = 'consistent_homologation_capture'
        $captureArgs = @('-NoProfile', '-NonInteractive', '-File', $config.CaptureMaintenance,
            '-ConfigPath', $config.SourceConfig, '-StagingRoot', $config.StagingRoot, '-BackupId', $backupId)
        $capture = (Invoke-CaptureProcess $captureArgs) | ConvertFrom-Json
        if ($capture.backupId -ne $backupId -or -not $capture.consistent -or -not $capture.sourceUnchanged) { throw 'capture_invariants_failed' }
        $sourceSet = Join-Path $config.StagingRoot $backupId
        $archive = Join-Path $config.StagingRoot ($backupId + '.tar')
        $phase = 'archive'
        $null = Invoke-PrivateProcess $config.Tools.TarExe @('-cf', $archive, '-C', $sourceSet, '.')
        $phase = 'age_encryption'
        $ciphertext = Join-Path $config.StagingRoot ($backupId + '.age')
        $null = Invoke-PrivateProcess $crypto.AgeExe @('--encrypt', '-R', $crypto.RecipientsFile, '--output', $ciphertext, $archive)
        $cipherInfo = Get-Item -LiteralPath $ciphertext
        $cipherHash = (Get-FileHash -LiteralPath $ciphertext -Algorithm SHA256).Hash.ToLowerInvariant()
        $phase = 'approved_devops_transport'
        $receipt = Invoke-RemoteJson $crypto.RemoteReceiver @('--receive', '--backup-id', $backupId, '--sha256', $cipherHash,
            '--bytes', $cipherInfo.Length.ToString([Globalization.CultureInfo]::InvariantCulture), '--format', 'age') $ciphertext
        if ($receipt.backupId -ne $backupId -or $receipt.ciphertextSha256 -ne $cipherHash -or
            [long]$receipt.bytes -ne $cipherInfo.Length -or -not $receipt.transportIntegrity) { throw 'transport_receipt_mismatch' }
        $phase = 'independent_decryption_and_integrity'
        $verification = Invoke-RemoteJson $crypto.RemoteVerifier @('--backup-id', $backupId, '--sha256', $cipherHash)
        if (-not $verification.decryptionVerified -or -not $verification.packageIntegrityVerified -or
            $verification.backupId -ne $backupId) { throw 'remote_decryption_or_integrity_failed' }
        $phase = 'remove_plaintext_staging'
        Remove-OwnedStage $sourceSet $config.StagingRoot $backupId
        Remove-OwnedStage $archive $config.StagingRoot ($backupId + '.tar')
        Remove-OwnedStage $ciphertext $config.StagingRoot ($backupId + '.age')
        $phase = 'retention'
        $retentionApplied = $false
        if ($config.Retention.Enabled -and $config.Recovery.InitialRestoreVerified) {
            $retention = Invoke-RemoteJson $crypto.RemoteRetention @('--apply', '--protect', $backupId,
                '--restore-protect', $config.Recovery.LastRestoredBackupId)
            if (-not $retention.ok) { throw 'retention_failed' }
            $retentionApplied = $true
        }
        $status = [ordered]@{task=475;ok=$true;backupId=$backupId;sourceAlias='PILOT-472-CLEAN';destinationAlias='DEVOPS-GENSW-MAIN';
            localDate=$localDate;executionMode=$(if($Scheduled){'Scheduled'}else{'Manual'});tableCount=$capture.tableCount;
            imageCount=$capture.imageCount;sourceUnchanged=$true;transportIntegrity=$true;decryptionVerified=$true;
            packageIntegrityVerified=$true;ciphertextSha256=$cipherHash;plaintextStagingRemoved=$true;localCiphertextStagingRemoved=$true;
            retentionApplied=$retentionApplied;fullDatabaseRestoreThisRun=$false;finishedAtUtc=[DateTime]::UtcNow.ToString('o')}
        $status | ConvertTo-Json | Set-Content -LiteralPath $lastSuccessPath -Encoding utf8
        $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $privateRoot ($backupId + '.status.json')) -Encoding utf8
        $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $privateRoot 'last-run.json') -Encoding utf8
        $status | ConvertTo-Json -Compress
    } finally { $lock.Dispose() }
} catch {
    $failureDetail = $_
    $status = [ordered]@{task=475;ok=$false;backupId=$backupId;phase=$phase;reviewRequired=$true;finishedAtUtc=[DateTime]::UtcNow.ToString('o')}
    if ($privateRoot) {
        $status | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $privateRoot 'last-run.json') -Encoding utf8
        $failureDetail | Out-String | Set-Content -LiteralPath (Join-Path $privateRoot 'logs\last-failure-private.log') -Encoding utf8
    }
    $status | ConvertTo-Json -Compress
    exit 1
}
