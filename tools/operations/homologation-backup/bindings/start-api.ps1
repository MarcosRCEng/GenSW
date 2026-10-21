param([Parameter(Mandatory = $true)][string]$WorkspaceRoot)
$ErrorActionPreference='Stop'
$taskCfg=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'runtime.json') -Raw | ConvertFrom-Json
$taskApiRoot=Join-Path $WorkspaceRoot 'src/Backend/GenSW.API'
Add-Type -Path (Join-Path $taskApiRoot 'bin/Debug/net8.0/Npgsql.dll')
$taskSource=[Npgsql.NpgsqlConnectionStringBuilder]::new()
$taskSource.set_ConnectionString($taskCfg.sourceConnection)
$taskCopy=[Npgsql.NpgsqlConnectionStringBuilder]::new()
$taskCopy.set_ConnectionString($taskCfg.validationConnection)
# Explicit CLR methods are required: PowerShell's dictionary adaptation makes
# .ConnectionString ambiguous on DbConnectionStringBuilder-derived objects.
$taskSourceDatabase=$taskSource.get_Database()
$taskCopyDatabase=$taskCopy.get_Database()
if($taskSourceDatabase -cne $taskCfg.sourceDatabase -or $taskCopyDatabase -cne $taskCfg.validationDatabase -or $taskCopyDatabase -ceq $taskSourceDatabase -or $taskCopyDatabase -notmatch '^gensw_pilot_472_'){throw 'Typed database target guard rejected runtime.'}
if($taskCopy.get_Host() -cne $taskSource.get_Host() -or $taskCopy.get_Port() -ne $taskSource.get_Port() -or $taskCopy.get_Username() -cne $taskSource.get_Username()){throw 'Typed server, port or user guard rejected runtime.'}
if($taskCopy.get_ApplicationName() -cne 'GenSW-PILOT-473-API'){throw 'Exclusive application name is required.'}
$taskRoundtrip=[Npgsql.NpgsqlConnectionStringBuilder]::new()
$taskRoundtrip.set_ConnectionString($taskCopy.get_ConnectionString())
if($taskRoundtrip.get_Database() -cne $taskCfg.validationDatabase){throw 'Serialized target guard failed.'}
if(!(Test-Path -LiteralPath $taskCfg.validationImages -PathType Container)){throw 'Restored private image volume unavailable.'}
if(!(Get-Acl -LiteralPath $PSScriptRoot).AreAccessRulesProtected){throw 'Private configuration directory must have protected ACL inheritance.'}
if(Get-NetTCPConnection -State Listen -LocalPort 7003 -ErrorAction SilentlyContinue){throw 'Pilot API port occupied; inventory before starting.'}
Set-Location -LiteralPath $WorkspaceRoot
if((git rev-parse HEAD).Trim() -cne '84d7cc5f44656c8d34865e73fb48f4155ab3585c'){throw 'Task baseline differs.'}
$env:ConnectionStrings__GenSW=$taskCopy.get_ConnectionString()
$env:Authentication__Jwt__SigningKey=$taskCfg.signingKey
$env:Images__PrivateRoot=$taskCfg.validationImages
$env:Cors__AllowedOrigins__0='https://localhost:5175'
$env:ASPNETCORE_ENVIRONMENT='Development'
$env:Logging__LogLevel__Default='Warning'
$env:Logging__LogLevel__Microsoft='Warning'
$taskDll=Join-Path $taskApiRoot 'bin/Debug/net8.0/GenSW.API.dll'
$taskApi=Start-Process -FilePath 'dotnet.exe' -ArgumentList @(('"'+$taskDll+'"'),'--urls','https://localhost:7003') -WorkingDirectory $taskApiRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $PSScriptRoot 'api-runtime-stdout.log') -RedirectStandardError (Join-Path $PSScriptRoot 'api-runtime-stderr.log') -PassThru
@{apiProcessId=$taskApi.Id;apiStartTimeUtc=$taskApi.StartTime.ToUniversalTime().ToString('o');apiPort=7003;databaseAlias='PILOT-472-CLEAN';applicationName='GenSW-PILOT-473-API'} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot 'api-process.json') -Encoding utf8
@{started=$true;processId=$taskApi.Id;api='https://localhost:7003';typedDatabaseTargetGuardsPassed=$true;exclusiveApplicationNameConfigured=$true;effectiveRuntimeDatabaseStillRequiresIndependentObservation=$true} | ConvertTo-Json -Compress
