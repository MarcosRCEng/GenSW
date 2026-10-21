$ErrorActionPreference='Stop'
$taskState=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'api-process.json') -Raw | ConvertFrom-Json
if($taskState.apiPort -ne 7003 -or $taskState.databaseAlias -ne 'PILOT-472-CLEAN'){throw 'Unexpected pilot process metadata.'}
$taskListeners=@(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue)
if(!$taskListeners.Count){Write-Output 'Pilot API is already stopped.';exit 0}
$taskPids=@($taskListeners.OwningProcess | Select-Object -Unique)
if($taskPids.Count -ne 1 -or $taskPids[0] -ne $taskState.apiProcessId){throw 'Listener ownership changed; do not stop an unverified process.'}
$taskProcess=Get-Process -Id $taskState.apiProcessId
$taskExpectedStart=[DateTimeOffset]$taskState.apiStartTimeUtc
$taskActualStart=[DateTimeOffset]$taskProcess.StartTime
if($taskActualStart.UtcDateTime.Ticks -ne $taskExpectedStart.UtcDateTime.Ticks){throw 'Process identity changed; do not stop an unverified process.'}
Stop-Process -Id $taskProcess.Id
Write-Output 'Only the verified pilot API process was stopped; P0 preserved.'
