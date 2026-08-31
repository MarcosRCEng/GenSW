[CmdletBinding()]
param(
    [switch]$CheckOnly,
    [switch]$SkipAdminBootstrap
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'gensw-local-common.ps1')

function Invoke-GenSWAdminBootstrapPrompt {
    $answer = Read-Host 'Deseja executar o bootstrap explícito do administrador inicial? [S/N]'
    if ($answer -notmatch '^[Ss]$') {
        Write-GenSWInfo 'Bootstrap administrativo não solicitado.'
        return
    }

    $name = Read-Host 'Nome do administrador'
    $username = Read-Host 'Username do administrador'
    Write-Host 'Senha do administrador (não será exibida):' -ForegroundColor Yellow
    $securePassword = Read-Host -AsSecureString
    $password = ConvertFrom-GenSWSecureString -SecureValue $securePassword

    if ([string]::IsNullOrWhiteSpace($name) -or
        [string]::IsNullOrWhiteSpace($username) -or
        [string]::IsNullOrWhiteSpace($password)) {
        $password = $null
        throw 'Nome, username e senha são obrigatórios para o bootstrap.'
    }

    $connectionString = Get-GenSWSecretValue -Name 'ConnectionStrings:GenSW'
    $previousConnectionString = $env:ConnectionStrings__GenSW
    $previousName = $env:InitialAdminBootstrap__Name
    $previousUsername = $env:InitialAdminBootstrap__Username
    $previousPassword = $env:InitialAdminBootstrap__Password

    $exitCode = -1
    try {
        $env:ConnectionStrings__GenSW = $connectionString
        $env:InitialAdminBootstrap__Name = $name
        $env:InitialAdminBootstrap__Username = $username
        $env:InitialAdminBootstrap__Password = $password

        Push-Location -LiteralPath $script:GenSWRoot
        try {
            & dotnet run --project $script:GenSWAdminBootstrapProject --no-restore
            $exitCode = $LASTEXITCODE
        }
        finally {
            Pop-Location
        }

        if ($exitCode -eq 2) {
            Write-GenSWWarning 'O bootstrap foi recusado porque a base já possui usuário. Nenhuma alteração adicional foi feita.'
            return
        }

        if ($exitCode -ne 0) {
            throw "O bootstrap administrativo falhou com exit code $exitCode."
        }

        Write-GenSWInfo 'Administrador inicial provisionado.'
    }
    finally {
        $env:ConnectionStrings__GenSW = $previousConnectionString
        $env:InitialAdminBootstrap__Name = $previousName
        $env:InitialAdminBootstrap__Username = $previousUsername
        $env:InitialAdminBootstrap__Password = $previousPassword
        $connectionString = $null
        $password = $null
    }
}

try {
    Write-GenSWInfo 'Validando pré-requisitos locais...'
    Assert-GenSWPrerequisites
    Assert-GenSWFixedConfiguration
    Assert-GenSWSensitiveArtifactsIgnored

    if ($CheckOnly) {
        Assert-GenSWCertificate
        Assert-GenSWRequiredSecrets
        Assert-GenSWFrontendEnvLocal

        $nodeModules = Join-Path $script:GenSWFrontendDirectory 'node_modules'
        if (-not (Test-Path -LiteralPath $nodeModules -PathType Container)) {
            throw 'As dependências frontend não estão instaladas. Execute setup-gensw.bat sem -CheckOnly.'
        }

        Test-GenSWDatabaseConnection
        Write-Host ''
        Write-Host 'Validação do setup concluída com sucesso.' -ForegroundColor Green
        exit 0
    }

    Initialize-GenSWCertificate
    Initialize-GenSWUserSecrets
    Update-GenSWFrontendEnvLocal
    Assert-GenSWRequiredSecrets
    Assert-GenSWFrontendEnvLocal
    Test-GenSWDatabaseConnection

    Write-GenSWInfo 'Restaurando dependências .NET...'
    Invoke-GenSWExternalCommand `
        -FilePath 'dotnet' `
        -ArgumentList @('restore', 'GenSW.sln') `
        -WorkingDirectory $script:GenSWRoot `
        -FailureMessage 'dotnet restore GenSW.sln falhou.'

    Write-GenSWInfo 'Restaurando ferramentas .NET locais...'
    Invoke-GenSWExternalCommand `
        -FilePath 'dotnet' `
        -ArgumentList @('tool', 'restore') `
        -WorkingDirectory $script:GenSWRoot `
        -FailureMessage 'dotnet tool restore falhou.'

    Write-GenSWInfo 'Instalando dependências frontend com npm ci...'
    Invoke-GenSWExternalCommand `
        -FilePath 'npm.cmd' `
        -ArgumentList @('ci') `
        -WorkingDirectory $script:GenSWFrontendDirectory `
        -FailureMessage 'npm ci falhou.'

    Invoke-GenSWMigrations

    if (-not $SkipAdminBootstrap) {
        Invoke-GenSWAdminBootstrapPrompt
    }
    else {
        Write-GenSWInfo 'Bootstrap administrativo omitido por solicitação explícita.'
    }

    Assert-GenSWLocalSetup
    Test-GenSWDatabaseConnection

    Write-Host ''
    Write-Host 'Setup do GenSW concluído.' -ForegroundColor Green
    Write-Host 'Use start-gensw.bat para iniciar a aplicação.'
    exit 0
}
catch {
    Write-Host ''
    Write-GenSWFailure $_.Exception.Message
    exit 1
}
