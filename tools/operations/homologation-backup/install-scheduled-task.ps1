param([Parameter(Mandatory = $true)][string]$ConfigPath)
$ErrorActionPreference = 'Stop'
try {
    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if ($config.Environment -ne 'Homologation' -or -not $config.Enabled -or -not $config.Schedule.Enabled -or
        -not $config.Recovery.InitialRestoreVerified -or $config.SourceAlias -ne 'PILOT-472-CLEAN' -or
        $config.Schedule.TaskName -ne 'GenSW-Homologation-Backup') { throw 'validated_homologation_configuration_required' }
    if ([TimeZoneInfo]::Local.Id -ne 'E. South America Standard Time') { throw 'timezone_mismatch' }
    foreach ($path in @($ConfigPath, $config.DailyRunner)) {
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw 'private_file_missing' }
        $item = Get-Item -LiteralPath $path -Force
        if ($item.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw 'private_file_reparse_point' }
        $acl = Get-Acl -LiteralPath $path
        if (-not $acl.AreAccessRulesProtected) { throw 'private_acl_required' }
        $allowed = @([Security.Principal.WindowsIdentity]::GetCurrent().User.Value, 'S-1-5-18', 'S-1-5-32-544')
        foreach ($rule in $acl.Access) {
            $sid = $rule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
            if ($rule.AccessControlType -eq 'Allow' -and $sid -notin $allowed) { throw 'private_acl_unapproved_principal' }
        }
    }
    foreach ($path in @($config.DailyRunner, $ConfigPath, $config.Tools.PowerShellExe)) {
        if ($path.Contains('"') -or $path.Contains("`r") -or $path.Contains("`n")) { throw 'invalid_path' }
    }
    $existing = Get-ScheduledTask -TaskName $config.Schedule.TaskName -ErrorAction SilentlyContinue
    if ($existing) {
        $ownedRunner = $existing.Actions[0].Arguments.Contains($config.DailyRunner) -or
            ($config.TaskContext.PreviousDailyRunner -and
                $existing.Actions[0].Arguments.Contains($config.TaskContext.PreviousDailyRunner))
        if ($existing.Description -notlike 'GenSW homologation backup managed by task 475*' -or
            @($existing.Actions).Count -ne 1 -or $existing.Actions[0].Execute -ne $config.Tools.PowerShellExe -or
            -not $ownedRunner) { throw 'existing_task_not_owned' }
    }
    $arguments = '-NoProfile -NonInteractive -WindowStyle Hidden -File "' + $config.DailyRunner + '" -ConfigPath "' + $ConfigPath + '" -Scheduled'
    $action = New-ScheduledTaskAction -Execute $config.Tools.PowerShellExe -Argument $arguments -WorkingDirectory $config.PrivateRoot
    $trigger = New-ScheduledTaskTrigger -Daily -At '03:00'
    $principal = New-ScheduledTaskPrincipal -UserId ([Security.Principal.WindowsIdentity]::GetCurrent().User.Value) -LogonType Interactive -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -MultipleInstances IgnoreNew -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Hours 2) `
        -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 15) -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries
    $settings.Enabled = $true
    $task = New-ScheduledTask -Action $action -Trigger $trigger -Principal $principal -Settings $settings `
        -Description 'GenSW homologation backup managed by task 475; daily 03:00 Sao Paulo, encrypted DevOPS destination, private configuration, logged-on operator required.'
    Register-ScheduledTask -TaskName $config.Schedule.TaskName -InputObject $task -Force | Out-Null
    $registered = Get-ScheduledTask -TaskName $config.Schedule.TaskName
    [xml]$exported = Export-ScheduledTask -TaskName $config.Schedule.TaskName
    if (-not $registered.Settings.Enabled -or $registered.Principal.LogonType -ne 'Interactive' -or
        $registered.Actions[0].WorkingDirectory -ne $config.PrivateRoot -or
        $exported.Task.Principals.Principal.LogonType -ne 'InteractiveToken') {
        throw 'registration_verification_failed'
    }
    @{task=475;registered=$true;enabled=$true;dailyTime='03:00';timezone='America/Sao_Paulo';
        principalMode='InteractiveToken';requiresLoggedOnUser=$true;startWhenAvailable=$true;credentialsInTaskDefinition=$false} | ConvertTo-Json -Compress
} catch {
    @{task=475;registered=$false;reviewRequired=$true;errorType=$_.Exception.GetType().Name} | ConvertTo-Json -Compress
    exit 1
}
