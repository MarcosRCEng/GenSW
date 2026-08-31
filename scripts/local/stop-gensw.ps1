[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'gensw-local-common.ps1')

try {
    $runtimeState = Get-GenSWValidatedRuntimeState
    if ($runtimeState.Valid.Count -eq 0) {
        Remove-GenSWRuntimeState
        Write-Host 'Nenhuma instância iniciada pelo launcher foi encontrada.' -ForegroundColor Green
        exit 0
    }

    Stop-GenSWProcessRecords -Records $runtimeState.Valid
    Remove-GenSWRuntimeState

    Write-Host ''
    Write-Host 'GenSW encerrado.' -ForegroundColor Green
    Write-Host 'O PostgreSQL não foi alterado.'
    exit 0
}
catch {
    Write-Host ''
    Write-GenSWFailure $_.Exception.Message
    exit 1
}
