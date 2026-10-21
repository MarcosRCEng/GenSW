param(
    [Parameter(Mandatory=$true)][string]$ConfigPath,
    [Parameter(Mandatory=$true)][string]$StagingRoot,
    [Parameter(Mandatory=$true)][string]$BackupId
)
$ErrorActionPreference='Stop'
$taskStopped=$false
$taskFailure=$null
$taskCapture=$null
$taskMutex=$null
$taskLockHeld=$false
try {
    if($BackupId -notmatch '^gensw-[A-Za-z0-9][A-Za-z0-9_-]{0,73}$'){throw 'Unsafe backup identifier.'}
    $taskPrivate=(Resolve-Path -LiteralPath $PSScriptRoot).Path
    $taskCfg=Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if($taskCfg.task -ne 475 -or $taskCfg.SourceAlias -cne 'PILOT-472-CLEAN' -or $taskCfg.Environment -cne 'Homologation'){throw 'Approved served Homologation configuration required.'}
    $taskStageFull=[IO.Path]::GetFullPath($StagingRoot)
    if(!$taskStageFull.StartsWith($taskPrivate+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Staging must be inside the private task directory.'}
    if(!(Test-Path -LiteralPath $taskStageFull)){New-Item -ItemType Directory -Path $taskStageFull | Out-Null}
    if(((Get-Item -LiteralPath $taskStageFull).Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0){throw 'Staging links are forbidden.'}
    $taskPrivateAcl=Get-Acl -LiteralPath $taskPrivate
    if(!$taskPrivateAcl.AreAccessRulesProtected){throw 'Private directory ACL inheritance must be protected.'}
    # Validate the preconfigured DACL. Copying a full security descriptor can
    # require SACL/owner privileges unavailable to the scheduler user.
    $taskAcl=Get-Acl -LiteralPath $taskStageFull
    $taskUserSid=[Security.Principal.WindowsIdentity]::GetCurrent().User.Value
    $taskAllowedSids=@($taskUserSid,'S-1-5-18','S-1-5-32-544')
    foreach($taskRule in $taskAcl.Access){
        $taskRuleSid=$taskRule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if($taskRule.AccessControlType -eq 'Allow' -and $taskRuleSid -notin $taskAllowedSids){throw 'Staging ACL grants an unapproved principal.'}
    }
    if(!$taskAcl.AreAccessRulesProtected){throw 'Staging ACL inheritance is not protected.'}
    $taskLogRoot=Join-Path $taskPrivate ('maintenance-'+$BackupId)
    if(Test-Path -LiteralPath $taskLogRoot){throw 'Maintenance log identifier already exists.'}
    New-Item -ItemType Directory -Path $taskLogRoot | Out-Null
    # The log directory inherits the already protected private task parent's
    # user-only DACL; no owner/SACL rewrite or elevation is needed.
    $taskLogAcl=Get-Acl -LiteralPath $taskLogRoot
    foreach($taskRule in $taskLogAcl.Access){
        $taskRuleSid=$taskRule.IdentityReference.Translate([Security.Principal.SecurityIdentifier]).Value
        if($taskRule.AccessControlType -eq 'Allow' -and $taskRuleSid -notin $taskAllowedSids){throw 'Private log ACL grants an unapproved principal.'}
    }
    $taskMutex=[Threading.Mutex]::new($false,'Local\GenSW-PILOT-475-MAINTENANCE')
    $taskLockHeld=$taskMutex.WaitOne(0)
    if(!$taskLockHeld){throw 'Another maintenance capture is active.'}
    $taskReference=Get-Content -LiteralPath $taskCfg.SourceRuntimePath -Raw | ConvertFrom-Json
    Add-Type -Path (Join-Path $taskCfg.WorkspaceRoot 'src/Backend/GenSW.API/bin/Debug/net8.0/Npgsql.dll')
    $taskSource=[Npgsql.NpgsqlConnectionStringBuilder]::new()
    $taskSource.set_ConnectionString($taskCfg.sourceConnection)
    $taskExpected=[Npgsql.NpgsqlConnectionStringBuilder]::new()
    $taskExpected.set_ConnectionString($taskReference.validationConnection)
    $taskLegacy=[Npgsql.NpgsqlConnectionStringBuilder]::new()
    $taskLegacy.set_ConnectionString($taskReference.sourceConnection)
    if($taskSource.get_Database() -cne $taskCfg.sourceDatabase -or $taskSource.get_Database() -cne $taskCfg.approvedHomologDatabase -or $taskSource.get_Database() -cne $taskExpected.get_Database() -or $taskSource.get_Database() -ceq $taskLegacy.get_Database() -or $taskSource.get_Database() -in $taskCfg.forbiddenP0Databases){throw 'Typed source database guard failed.'}
    if($taskSource.get_Host() -cne $taskExpected.get_Host() -or $taskSource.get_Port() -ne $taskExpected.get_Port() -or $taskSource.get_Username() -cne $taskExpected.get_Username() -or $taskSource.get_Username() -cne $taskCfg.pg.user -or $taskSource.get_Password() -cne $taskCfg.pg.password){throw 'Typed source server or credentials guard failed.'}
    if([IO.Path]::GetFullPath($taskCfg.sourceImages) -cne [IO.Path]::GetFullPath($taskReference.validationImages) -or [IO.Path]::GetFullPath($taskCfg.sourceImages) -cne [IO.Path]::GetFullPath($taskCfg.approvedHomologImages) -or [IO.Path]::GetFullPath($taskCfg.sourceImages) -ceq [IO.Path]::GetFullPath($taskCfg.legacyImages)){throw 'Exact source image volume guard failed.'}
    # Verify the exact migration history on the guarded homologation target
    # before starting any API. No migrations or data writes are performed here.
    $taskExpectedMigrations=@(Get-ChildItem -LiteralPath (Join-Path $taskCfg.WorkspaceRoot 'src/Backend/GenSW.Infrastructure/Persistence/Migrations') -File -Filter '*.cs' | Where-Object {$_.Name -match '^\d{14}_[A-Za-z0-9_]+\.cs$'} | Select-Object -ExpandProperty BaseName | Sort-Object)
    if($taskExpectedMigrations.Count -ne 18){throw 'Approved revision must have exactly eighteen migrations.'}
    $taskPreflightBuilder=[Npgsql.NpgsqlConnectionStringBuilder]::new()
    $taskPreflightBuilder.set_ConnectionString($taskSource.get_ConnectionString())
    $taskPreflightBuilder.set_Pooling($false)
    $taskPreflightBuilder.set_ApplicationName('GenSW-PILOT-475-MIGRATIONS')
    $taskPreflightConnection=[Npgsql.NpgsqlConnection]::new($taskPreflightBuilder.get_ConnectionString())
    try {
        $taskPreflightConnection.Open()
        $taskPreflightTransaction=$taskPreflightConnection.BeginTransaction([Data.IsolationLevel]::RepeatableRead)
        $taskPreflightCommand=$taskPreflightConnection.CreateCommand()
        $taskPreflightCommand.Transaction=$taskPreflightTransaction
        try {
            $taskPreflightCommand.CommandText='SET TRANSACTION READ ONLY'
            [void]$taskPreflightCommand.ExecuteNonQuery()
            $taskPreflightCommand.CommandText='SELECT current_database(), current_user'
            $taskPreflightReader=$taskPreflightCommand.ExecuteReader()
            try {
                if(!$taskPreflightReader.Read() -or $taskPreflightReader.GetString(0) -cne $taskCfg.approvedHomologDatabase -or $taskPreflightReader.GetString(1) -cne $taskCfg.pg.user){throw 'Migration preflight effective target rejected.'}
            } finally {$taskPreflightReader.Dispose()}
            $taskPreflightCommand.CommandText='SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId" COLLATE "C"'
            $taskPreflightReader=$taskPreflightCommand.ExecuteReader()
            $taskObservedMigrations=[Collections.Generic.List[string]]::new()
            try {while($taskPreflightReader.Read()){$taskObservedMigrations.Add($taskPreflightReader.GetString(0))}} finally {$taskPreflightReader.Dispose()}
            if(($taskObservedMigrations -join "`n") -cne ($taskExpectedMigrations -join "`n")){throw 'Migration history differs from the approved revision.'}
            $taskPreflightTransaction.Rollback()
        } finally {$taskPreflightCommand.Dispose();$taskPreflightTransaction.Dispose()}
    } finally {$taskPreflightConnection.Dispose()}
    function Assert-Approved475Listener {
        $taskIdentity=Get-Content -LiteralPath $taskCfg.ApiProcessStatePath -Raw | ConvertFrom-Json
        if($taskIdentity.apiPort -ne 7003 -or $taskIdentity.databaseAlias -cne 'PILOT-472-CLEAN' -or $taskIdentity.applicationName -cne $taskCfg.servedApiApplicationName){throw 'Unexpected API process identity metadata.'}
        $taskListeningPids=@(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique)
        if($taskListeningPids.Count -ne 1 -or $taskListeningPids[0] -ne $taskIdentity.apiProcessId){throw 'Foreign or absent API listener; refusing diagnostic requests and maintenance.'}
        $taskLiveProcess=Get-Process -Id $taskListeningPids[0]
        if($taskLiveProcess.StartTime.ToUniversalTime().Ticks -ne ([DateTimeOffset]$taskIdentity.apiStartTimeUtc).UtcDateTime.Ticks){throw 'API process identity changed; refusing maintenance.'}
    }
    $taskStartedForBackup=$false
    if(!(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue)){
        # After reboot the daily job may precede interactive application startup.
        # The existing launcher independently enforces the exact typed target.
        & $taskCfg.StartLauncher -WorkspaceRoot $taskCfg.WorkspaceRoot *> (Join-Path $taskLogRoot 'startup-private.log')
        $taskStartupReady=$false
        for($taskStartupAttempt=0;$taskStartupAttempt -lt 100;$taskStartupAttempt++){
            if(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue){$taskStartupReady=$true;break}
            Start-Sleep -Milliseconds 250
        }
        if(!$taskStartupReady){throw 'Homologation API autostart did not become ready.'}
        $taskStartedForBackup=$true
    }
    Assert-Approved475Listener
    @{migrationCount=$taskExpectedMigrations.Count;effectiveHomologationTargetVerified=$true;readOnlyPreflight=$true;apiStartedForBackup=$taskStartedForBackup;listenerIdentityVerifiedBeforeProbe=$true} | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $taskLogRoot 'migration-startup-preflight.json') -Encoding utf8
    $taskBeforeRaw=& $taskCfg.PythonExe $taskCfg.RuntimeVerifierScript --config $ConfigPath --report (Join-Path $taskLogRoot 'runtime-before-stop.json') --allow-negative-probe 2> (Join-Path $taskLogRoot 'runtime-before-stop.stderr.log')
    if($LASTEXITCODE -ne 0){throw 'Effective source runtime proof failed.'}
    ($taskBeforeRaw -join [Environment]::NewLine) | Set-Content -LiteralPath (Join-Path $taskLogRoot 'runtime-before-stop.stdout.json') -Encoding utf8
    $taskBefore=($taskBeforeRaw -join [Environment]::NewLine) | ConvertFrom-Json
    if(!$taskBefore.apiPidSocketPostgresChainPassed -or !$taskBefore.allApiSessionsUseApprovedHomologation -or $taskBefore.observedApiSessionsInLegacy -ne 0){throw 'Effective source runtime chain rejected.'}
    try {
        & $taskCfg.StopLauncher *> (Join-Path $taskLogRoot 'stop-private.log')
        $taskPortReleased=$false
        for($taskStopAttempt=0;$taskStopAttempt -lt 40;$taskStopAttempt++){
            if(!(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue)){$taskPortReleased=$true;break}
            Start-Sleep -Milliseconds 250
        }
        if(!$taskPortReleased){throw 'API did not release its listener for maintenance.'}
        $taskStopped=$true
        $taskRaw=& $taskCfg.PythonExe $taskCfg.CaptureScript --config $ConfigPath --output-root $taskStageFull --backup-id $BackupId 2> (Join-Path $taskLogRoot 'capture.stderr.log')
        ($taskRaw -join [Environment]::NewLine) | Set-Content -LiteralPath (Join-Path $taskLogRoot 'capture.stdout.json') -Encoding utf8
        if($LASTEXITCODE -ne 0){throw 'Source capture failed.'}
        $taskCapture=($taskRaw -join [Environment]::NewLine) | ConvertFrom-Json
        if(!$taskCapture.consistent -or !$taskCapture.sourceUnchanged -or $taskCapture.backupId -cne $BackupId){throw 'Capture summary was rejected.'}
    }
    finally {
        # The preflight chain already proved the original listener identity.
        # Even if StopLauncher stops it and then throws before our flag is set,
        # a missing listener must be restored by the guarded start entrypoint.
        $taskListenerAfterAttempt=@(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue)
        if($taskListenerAfterAttempt.Count -eq 0){
            $taskStopped=$true
            & $taskCfg.StartLauncher -WorkspaceRoot $taskCfg.WorkspaceRoot *> (Join-Path $taskLogRoot 'restart-private.log')
            $taskReady=$false
            for($taskAttempt=0;$taskAttempt -lt 40;$taskAttempt++){
                if(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue){$taskReady=$true;break}
                Start-Sleep -Milliseconds 250
            }
            if(!$taskReady){throw 'Homologation API restart did not become ready.'}
        }
        if(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue){
            Assert-Approved475Listener
            $taskAfterRaw=& $taskCfg.PythonExe $taskCfg.RuntimeVerifierScript --config $ConfigPath --report (Join-Path $taskLogRoot 'runtime-after-restart.json') --allow-negative-probe 2> (Join-Path $taskLogRoot 'runtime-after-restart.stderr.log')
            ($taskAfterRaw -join [Environment]::NewLine) | Set-Content -LiteralPath (Join-Path $taskLogRoot 'runtime-after-restart.stdout.json') -Encoding utf8
            if($LASTEXITCODE -ne 0){throw 'Restarted API effective target proof failed.'}
            $taskAfter=($taskAfterRaw -join [Environment]::NewLine) | ConvertFrom-Json
            if(!$taskAfter.apiPidSocketPostgresChainPassed -or !$taskAfter.allApiSessionsUseApprovedHomologation -or $taskAfter.observedApiSessionsInLegacy -ne 0){throw 'Restarted API target chain rejected.'}
            $taskStopped=$false
        }
    }
}
catch {
    $taskFailure=$_.Exception.GetType().Name
    if($taskPrivate -and (Test-Path -LiteralPath $taskPrivate)){
        @{errorType=$taskFailure;scriptLine=$_.InvocationInfo.ScriptLineNumber;apiWasStopped=$taskStopped} | ConvertTo-Json -Compress | Set-Content -LiteralPath (Join-Path $taskPrivate ('maintenance-failure-'+$BackupId+'.private.json')) -Encoding utf8
    }
}
finally {
    if($taskLockHeld){$taskMutex.ReleaseMutex()}
    if($taskMutex){$taskMutex.Dispose()}
}
if($taskFailure){@{status='FAILED';errorType=$taskFailure;details='Private operator review required; do not publish or delete staging.'} | ConvertTo-Json -Compress;exit 1}
@{backupId=$taskCapture.backupId;tableCount=$taskCapture.tableCount;imageCount=$taskCapture.imageCount;sourceUnchanged=$taskCapture.sourceUnchanged;consistent=$taskCapture.consistent} | ConvertTo-Json -Compress
