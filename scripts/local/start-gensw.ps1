[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$NoBrowser,
    [ValidateSet('Api', 'Web')]
    [string]$RunService
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'gensw-local-common.ps1')

function Invoke-GenSWServiceProcess {
    param([Parameter(Mandatory = $true)][ValidateSet('Api', 'Web')][string]$Kind)

    if ($Host.Name -eq 'ConsoleHost') {
        if ($Kind -eq 'Api') {
            $Host.UI.RawUI.WindowTitle = 'GenSW API'
        }
        else {
            $Host.UI.RawUI.WindowTitle = 'GenSW Web'
        }
    }

    if ($Kind -eq 'Api') {
        $env:ASPNETCORE_ENVIRONMENT = 'Development'
        Push-Location -LiteralPath $script:GenSWRoot
        try {
            & dotnet run --project $script:GenSWApiProject --launch-profile https
            return $LASTEXITCODE
        }
        finally {
            Pop-Location
        }
    }

    Push-Location -LiteralPath $script:GenSWFrontendDirectory
    try {
        & npm.cmd run dev
        return $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}

if (-not [string]::IsNullOrWhiteSpace($RunService)) {
    $serviceExitCode = Invoke-GenSWServiceProcess -Kind $RunService
    exit $serviceExitCode
}

$startedRecords = New-Object System.Collections.Generic.List[object]

try {
    Assert-GenSWLocalSetup
    Test-GenSWDatabaseConnection

    $runtimeState = Get-GenSWValidatedRuntimeState
    if ($runtimeState.HasApi -and $runtimeState.HasWeb) {
        Write-Host 'GenSW já está em execução.' -ForegroundColor Green
        Write-GenSWStartedSummary
        if (-not $NoBrowser -and -not $CheckOnly) {
            Start-Process $script:GenSWLoginUrl
        }
        exit 0
    }

    if ($runtimeState.Valid.Count -gt 0) {
        Write-GenSWWarning 'Foi encontrada uma inicialização parcial anterior do GenSW; somente os processos registrados serão encerrados.'
        Stop-GenSWProcessRecords -Records $runtimeState.Valid
    }
    Remove-GenSWRuntimeState

    Assert-GenSWApplicationPortsAvailable

    if ($CheckOnly) {
        Write-Host ''
        Write-Host 'Validação de startup concluída com sucesso.' -ForegroundColor Green
        exit 0
    }

    Invoke-GenSWMigrations

    Write-GenSWInfo 'Iniciando API em uma janela dedicada...'
    $apiRecord = Start-GenSWServiceWindow -Kind 'Api'
    $startedRecords.Add($apiRecord)
    Save-GenSWRuntimeState -Records $startedRecords.ToArray()

    Wait-GenSWHttpReadiness `
        -Name 'GenSW API' `
        -Uri $script:GenSWApiReadinessUrl `
        -ProcessRecord $apiRecord `
        -TimeoutSeconds 120

    Write-GenSWInfo 'Iniciando frontend em uma janela dedicada...'
    $webRecord = Start-GenSWServiceWindow -Kind 'Web'
    $startedRecords.Add($webRecord)
    Save-GenSWRuntimeState -Records $startedRecords.ToArray()

    Wait-GenSWHttpReadiness `
        -Name 'GenSW Web' `
        -Uri $script:GenSWWebUrl `
        -ProcessRecord $webRecord `
        -TimeoutSeconds 120

    if (-not $NoBrowser) {
        Write-GenSWInfo 'Abrindo o navegador na tela de login...'
        Start-Process $script:GenSWLoginUrl
    }

    Write-GenSWStartedSummary
    exit 0
}
catch {
    Write-Host ''
    Write-GenSWFailure $_.Exception.Message

    if ($startedRecords.Count -gt 0) {
        Write-GenSWWarning 'Limpando os processos iniciados por esta tentativa...'
        try {
            Stop-GenSWProcessRecords -Records $startedRecords.ToArray()
        }
        catch {
            Write-GenSWFailure $_.Exception.Message
        }
        finally {
            Remove-GenSWRuntimeState
        }
    }

    exit 1
}
