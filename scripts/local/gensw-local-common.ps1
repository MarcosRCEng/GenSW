Set-StrictMode -Version Latest

$script:GenSWWebHttpsPort = 7441
$script:GenSWApiHttpPort = 7442
$script:GenSWApiHttpsPort = 7443
$script:GenSWPostgreSqlPort = 5432

$script:GenSWRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
$script:GenSWApiProject = Join-Path $script:GenSWRoot 'src\Backend\GenSW.API\GenSW.API.csproj'
$script:GenSWInfrastructureProject = Join-Path $script:GenSWRoot 'src\Backend\GenSW.Infrastructure\GenSW.Infrastructure.csproj'
$script:GenSWAdminBootstrapProject = Join-Path $script:GenSWRoot 'src\Backend\GenSW.AdminBootstrap\GenSW.AdminBootstrap.csproj'
$script:GenSWFrontendDirectory = Join-Path $script:GenSWRoot 'src\Frontend\GenSW.Web'
$script:GenSWFrontendEnvLocal = Join-Path $script:GenSWFrontendDirectory '.env.local'
$script:GenSWRuntimeDirectory = Join-Path $script:GenSWRoot '.gensw\run'
$script:GenSWRuntimeStateFile = Join-Path $script:GenSWRuntimeDirectory 'processes.json'
$script:GenSWStartScript = Join-Path $PSScriptRoot 'start-gensw.ps1'

$script:GenSWWebUrl = "https://localhost:$script:GenSWWebHttpsPort"
$script:GenSWLoginUrl = "$script:GenSWWebUrl/login"
$script:GenSWApiUrl = "https://localhost:$script:GenSWApiHttpsPort"
$script:GenSWApiBaseUrl = "$script:GenSWApiUrl/api/v1"
$script:GenSWSwaggerUrl = "$script:GenSWApiUrl/swagger"
$script:GenSWApiReadinessUrl = "$script:GenSWApiUrl/swagger/v1/swagger.json"

function Write-GenSWInfo {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host "[GenSW] $Message" -ForegroundColor Cyan
}

function Write-GenSWWarning {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host "[GenSW] $Message" -ForegroundColor Yellow
}

function Write-GenSWFailure {
    param([Parameter(Mandatory = $true)][AllowEmptyString()][string]$Message)

    if ([string]::IsNullOrWhiteSpace($Message)) {
        $Message = 'Falha inesperada em um comando externo. Consulte a saída imediatamente anterior.'
    }

    Write-Host "[GenSW] $Message" -ForegroundColor Red
}

function Assert-GenSWCommand {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$InstallHint
    )

    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "O comando '$Name' não foi encontrado. $InstallHint"
    }
}

function Invoke-GenSWExternalCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$ArgumentList,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$FailureMessage
    )

    $exitCode = -1
    Push-Location -LiteralPath $WorkingDirectory
    try {
        & $FilePath @ArgumentList
        $exitCode = $LASTEXITCODE
    }
    finally {
        Pop-Location
    }

    if ($exitCode -ne 0) {
        throw "$FailureMessage Exit code: $exitCode."
    }
}

function Assert-GenSWPrerequisites {
    if ($PSVersionTable.PSVersion -lt [version]'5.1') {
        throw 'PowerShell 5.1 ou superior é obrigatório. Atualize o Windows PowerShell.'
    }

    Assert-GenSWCommand -Name 'dotnet' -InstallHint 'Instale o SDK .NET 8 ou superior em https://dotnet.microsoft.com/download.'
    Assert-GenSWCommand -Name 'node' -InstallHint 'Instale Node.js 20 LTS ou 22+ em https://nodejs.org/.'
    Assert-GenSWCommand -Name 'npm.cmd' -InstallHint 'Instale o npm junto com o Node.js.'

    $sdks = @(& dotnet --list-sdks 2>$null)
    $compatibleSdk = $false
    foreach ($sdk in $sdks) {
        if ($sdk -match '^(\d+)\.') {
            if ([int]$Matches[1] -ge 8) {
                $compatibleSdk = $true
                break
            }
        }
    }

    if (-not $compatibleSdk) {
        throw 'Nenhum SDK .NET compatível foi encontrado. Instale o SDK .NET 8 ou superior.'
    }

    $runtimes = @(& dotnet --list-runtimes 2>$null)
    $hasNetCoreEight = @($runtimes | Where-Object { $_ -match '^Microsoft\.NETCore\.App 8\.' }).Count -gt 0
    $hasAspNetCoreEight = @($runtimes | Where-Object { $_ -match '^Microsoft\.AspNetCore\.App 8\.' }).Count -gt 0
    if (-not $hasNetCoreEight -or -not $hasAspNetCoreEight) {
        throw 'Os runtimes Microsoft.NETCore.App 8 e Microsoft.AspNetCore.App 8 são obrigatórios. Instale o ASP.NET Core Runtime 8 x64.'
    }

    $nodeVersionText = (& node --version 2>$null).TrimStart('v')
    $nodeMajor = ([version]$nodeVersionText).Major
    if ($nodeMajor -ne 20 -and $nodeMajor -lt 22) {
        throw "Node.js $nodeVersionText não é suportado. Instale Node.js 20 LTS ou 22+."
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        # Windows PowerShell 5.1 converts even an empty native stderr record into
        # a terminating error when ErrorActionPreference is Stop.
        $ErrorActionPreference = 'Continue'
        & dotnet dev-certs https --help 2>$null | Out-Null
        $devCertificatesExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($devCertificatesExitCode -ne 0) {
        throw 'O suporte a dotnet dev-certs não está disponível. Instale um SDK .NET compatível.'
    }
}

function Get-GenSWCertificatePaths {
    $baseDirectory = $null
    if (-not [string]::IsNullOrWhiteSpace($env:APPDATA)) {
        $baseDirectory = Join-Path $env:APPDATA 'ASP.NET\https'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:USERPROFILE)) {
        $baseDirectory = Join-Path $env:USERPROFILE '.aspnet\https'
    }
    else {
        throw 'Não foi possível determinar um diretório local seguro para o certificado HTTPS.'
    }

    [pscustomobject]@{
        Directory = $baseDirectory
        Certificate = Join-Path $baseDirectory 'GenSW.Web.pem'
        Key = Join-Path $baseDirectory 'GenSW.Web.key'
    }
}

function Initialize-GenSWCertificate {
    Write-GenSWInfo 'Verificando e confiando no certificado HTTPS de desenvolvimento...'
    & dotnet dev-certs https --trust
    if ($LASTEXITCODE -ne 0) {
        throw 'Não foi possível confiar no certificado HTTPS. Execute dotnet dev-certs https --trust e tente novamente.'
    }

    $paths = Get-GenSWCertificatePaths
    New-Item -ItemType Directory -Path $paths.Directory -Force | Out-Null

    $temporaryCertificate = Join-Path $paths.Directory ("GenSW.Web.{0}.pem" -f [guid]::NewGuid().ToString('N'))
    $temporaryKey = [System.IO.Path]::ChangeExtension($temporaryCertificate, '.key')
    try {
        Write-GenSWInfo 'Exportando o certificado PEM local usado pelo Vite...'
        & dotnet dev-certs https --export-path $temporaryCertificate --format Pem --no-password
        if ($LASTEXITCODE -ne 0) {
            throw 'Não foi possível exportar o certificado HTTPS local para o Vite.'
        }

        if (-not (Test-Path -LiteralPath $temporaryCertificate -PathType Leaf) -or
            -not (Test-Path -LiteralPath $temporaryKey -PathType Leaf)) {
            throw 'A exportação HTTPS não produziu o par PEM esperado para o Vite.'
        }

        Move-Item -LiteralPath $temporaryCertificate -Destination $paths.Certificate -Force
        Move-Item -LiteralPath $temporaryKey -Destination $paths.Key -Force
    }
    finally {
        Remove-Item -LiteralPath $temporaryCertificate -Force -ErrorAction SilentlyContinue
        Remove-Item -LiteralPath $temporaryKey -Force -ErrorAction SilentlyContinue
    }

    Assert-GenSWCertificate
}

function Assert-GenSWCertificate {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        & dotnet dev-certs https --check --trust 2>$null | Out-Null
        $certificateCheckExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($certificateCheckExitCode -ne 0) {
        throw 'O certificado HTTPS de desenvolvimento não está confiável. Execute setup-gensw.bat primeiro.'
    }

    $paths = Get-GenSWCertificatePaths
    if (-not (Test-Path -LiteralPath $paths.Certificate -PathType Leaf) -or
        -not (Test-Path -LiteralPath $paths.Key -PathType Leaf) -or
        (Get-Item -LiteralPath $paths.Certificate).Length -eq 0 -or
        (Get-Item -LiteralPath $paths.Key).Length -eq 0) {
        throw 'O certificado PEM do GenSW não está preparado. Execute setup-gensw.bat primeiro.'
    }
}

function Get-GenSWUserSecrets {
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $output = @(& dotnet user-secrets list --project $script:GenSWApiProject --json 2>$null)
        $userSecretsExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($userSecretsExitCode -ne 0) {
        throw 'Não foi possível ler os User Secrets do GenSW. Execute setup-gensw.bat primeiro.'
    }

    $text = $output -join [Environment]::NewLine
    $jsonStart = $text.IndexOf('{')
    $jsonEnd = $text.LastIndexOf('}')
    if ($jsonStart -lt 0 -or $jsonEnd -lt $jsonStart) {
        return [pscustomobject]@{}
    }

    return ($text.Substring($jsonStart, $jsonEnd - $jsonStart + 1) | ConvertFrom-Json)
}

function Get-GenSWSecretValue {
    param([Parameter(Mandatory = $true)][string]$Name)

    $secrets = Get-GenSWUserSecrets
    $property = $secrets.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return [string]$property.Value
}

function ConvertFrom-GenSWSecureString {
    param([Parameter(Mandatory = $true)][Security.SecureString]$SecureValue)

    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($SecureValue)
    try {
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
}

function Set-GenSWUserSecret {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Value
    )

    $secretPayload = [ordered]@{}
    $secretPayload[$Name] = $Value
    $secretJson = $secretPayload | ConvertTo-Json -Compress
    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $secretJson | & dotnet user-secrets set --project $script:GenSWApiProject 2>$null | Out-Null
        $userSecretsSetExitCode = $LASTEXITCODE
        if ($userSecretsSetExitCode -ne 0) {
            throw "Não foi possível salvar '$Name' nos User Secrets do GenSW."
        }
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
        $secretJson = $null
        $secretPayload = $null
    }
}

function New-GenSWJwtSigningKey {
    $bytes = New-Object byte[] 48
    $generator = [Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $generator.GetBytes($bytes)
        return [Convert]::ToBase64String($bytes)
    }
    finally {
        $generator.Dispose()
        [Array]::Clear($bytes, 0, $bytes.Length)
    }
}

function Initialize-GenSWUserSecrets {
    $connectionString = Get-GenSWSecretValue -Name 'ConnectionStrings:GenSW'
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        if (-not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__GenSW)) {
            $connectionString = $env:ConnectionStrings__GenSW
            Set-GenSWUserSecret -Name 'ConnectionStrings:GenSW' -Value $connectionString
            $connectionString = $null
            Write-GenSWInfo 'Connection string externa consolidada com segurança nos User Secrets.'
        }
        else {
            Write-Host 'Informe a connection string PostgreSQL do GenSW. O valor não será exibido.' -ForegroundColor Yellow
            $secureConnectionString = Read-Host -AsSecureString
            $connectionString = ConvertFrom-GenSWSecureString -SecureValue $secureConnectionString
            if ([string]::IsNullOrWhiteSpace($connectionString)) {
                throw 'A connection string do GenSW é obrigatória.'
            }

            Set-GenSWUserSecret -Name 'ConnectionStrings:GenSW' -Value $connectionString
            $connectionString = $null
            Write-GenSWInfo 'Connection string salva com segurança nos User Secrets.'
        }
    }

    $signingKey = Get-GenSWSecretValue -Name 'Authentication:Jwt:SigningKey'
    if ([string]::IsNullOrWhiteSpace($signingKey)) {
        if (-not [string]::IsNullOrWhiteSpace($env:Authentication__Jwt__SigningKey)) {
            $signingKey = $env:Authentication__Jwt__SigningKey
        }
        else {
            $signingKey = New-GenSWJwtSigningKey
        }

        try {
            Set-GenSWUserSecret -Name 'Authentication:Jwt:SigningKey' -Value $signingKey
        }
        finally {
            $signingKey = $null
        }
        Write-GenSWInfo 'Chave JWT local segura salva nos User Secrets.'
    }
}

function Assert-GenSWRequiredSecrets {
    $connectionString = Get-GenSWSecretValue -Name 'ConnectionStrings:GenSW'
    $signingKey = Get-GenSWSecretValue -Name 'Authentication:Jwt:SigningKey'

    if ([string]::IsNullOrWhiteSpace($connectionString) -or
        [string]::IsNullOrWhiteSpace($signingKey)) {
        throw 'A configuração segura local está incompleta. Execute setup-gensw.bat primeiro.'
    }
}

function Get-GenSWDatabaseEndpoint {
    $connectionString = Get-GenSWSecretValue -Name 'ConnectionStrings:GenSW'
    if ([string]::IsNullOrWhiteSpace($connectionString)) {
        throw 'ConnectionStrings:GenSW não está configurada. Execute setup-gensw.bat primeiro.'
    }

    $builder = New-Object System.Data.Common.DbConnectionStringBuilder
    try {
        # DbConnectionStringBuilder implements IDictionary; PowerShell's property
        # adapter otherwise creates a literal "ConnectionString" dictionary key.
        $builder.set_ConnectionString($connectionString)
    }
    catch {
        throw 'ConnectionStrings:GenSW não possui um formato válido.'
    }

    $databaseHost = $null
    foreach ($key in @('Host', 'Server')) {
        if ($builder.ContainsKey($key)) {
            $databaseHost = [string]$builder[$key]
            break
        }
    }

    if ([string]::IsNullOrWhiteSpace($databaseHost)) {
        throw 'ConnectionStrings:GenSW deve informar Host ou Server.'
    }

    $port = $script:GenSWPostgreSqlPort
    if ($builder.ContainsKey('Port')) {
        $port = [int]$builder['Port']
    }

    [pscustomobject]@{
        Host = $databaseHost
        Port = $port
    }
}

function Test-GenSWDatabaseConnection {
    $endpoint = Get-GenSWDatabaseEndpoint
    if ($endpoint.Port -ne $script:GenSWPostgreSqlPort) {
        throw "O launcher local exige PostgreSQL na porta $script:GenSWPostgreSqlPort; a configuração segura aponta para a porta $($endpoint.Port)."
    }

    $reachable = Test-NetConnection -ComputerName $endpoint.Host -Port $endpoint.Port -InformationLevel Quiet -WarningAction SilentlyContinue
    if (-not $reachable) {
        throw "O PostgreSQL configurado para o GenSW não está acessível na porta $($endpoint.Port). Inicie a instância existente e tente novamente."
    }

    Write-GenSWInfo "PostgreSQL acessível na porta $($endpoint.Port); o launcher não gerenciará esse processo."
}

function Invoke-GenSWMigrations {
    Write-GenSWInfo 'Aplicando migrations do GenSW de forma idempotente...'
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        Invoke-GenSWExternalCommand `
            -FilePath 'dotnet' `
            -ArgumentList @(
                'ef', 'database', 'update',
                '--project', $script:GenSWInfrastructureProject,
                '--startup-project', $script:GenSWApiProject,
                '--context', 'GenSWDbContext'
            ) `
            -WorkingDirectory $script:GenSWRoot `
            -FailureMessage 'A atualização do banco falhou. Verifique a instância PostgreSQL e a configuração segura.'
    }
    finally {
        $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
    }
}

function Update-GenSWFrontendEnvLocal {
    $expectedLine = "VITE_API_BASE_URL=$script:GenSWApiBaseUrl"
    $lines = @()
    if (Test-Path -LiteralPath $script:GenSWFrontendEnvLocal -PathType Leaf) {
        $lines = @(Get-Content -LiteralPath $script:GenSWFrontendEnvLocal)
    }

    $updated = New-Object System.Collections.Generic.List[string]
    $found = $false
    foreach ($line in $lines) {
        if ($line -match '^\s*VITE_API_BASE_URL\s*=') {
            if (-not $found) {
                $updated.Add($expectedLine)
                $found = $true
            }
        }
        else {
            $updated.Add($line)
        }
    }

    if (-not $found) {
        if ($updated.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($updated[$updated.Count - 1])) {
            $updated.Add('')
        }
        $updated.Add($expectedLine)
    }

    [System.IO.File]::WriteAllLines(
        $script:GenSWFrontendEnvLocal,
        $updated,
        (New-Object System.Text.UTF8Encoding($false)))
}

function Assert-GenSWFrontendEnvLocal {
    if (-not (Test-Path -LiteralPath $script:GenSWFrontendEnvLocal -PathType Leaf)) {
        throw 'O arquivo frontend .env.local não existe. Execute setup-gensw.bat primeiro.'
    }

    $expected = "VITE_API_BASE_URL=$script:GenSWApiBaseUrl"
    $configured = @(@(Get-Content -LiteralPath $script:GenSWFrontendEnvLocal) | Where-Object { $_ -eq $expected })
    if ($configured.Count -ne 1) {
        throw "O .env.local deve conter exatamente '$expected'. Execute setup-gensw.bat novamente."
    }
}

function Assert-GenSWFixedConfiguration {
    $ports = @($script:GenSWWebHttpsPort, $script:GenSWApiHttpPort, $script:GenSWApiHttpsPort)
    if (@($ports | Select-Object -Unique).Count -ne 3) {
        throw 'As três portas de aplicação do GenSW devem ser fixas e distintas.'
    }

    $launchSettingsPath = Join-Path $script:GenSWRoot 'src\Backend\GenSW.API\Properties\launchSettings.json'
    $launchSettings = Get-Content -Raw -LiteralPath $launchSettingsPath | ConvertFrom-Json
    $expectedApplicationUrl = "https://localhost:$script:GenSWApiHttpsPort;http://localhost:$script:GenSWApiHttpPort"
    if ($launchSettings.profiles.https.applicationUrl -ne $expectedApplicationUrl) {
        throw 'launchSettings.json não corresponde às portas fixas do launcher.'
    }

    $developmentSettingsPath = Join-Path $script:GenSWRoot 'src\Backend\GenSW.API\appsettings.Development.json'
    $developmentSettings = Get-Content -Raw -LiteralPath $developmentSettingsPath | ConvertFrom-Json
    $origins = @($developmentSettings.Cors.AllowedOrigins)
    if ($origins.Count -ne 1 -or $origins[0] -ne $script:GenSWWebUrl) {
        throw 'O CORS de Development não corresponde à origem web fixa do GenSW.'
    }

    $viteConfigPath = Join-Path $script:GenSWFrontendDirectory 'vite.config.ts'
    $viteConfig = Get-Content -Raw -LiteralPath $viteConfigPath
    if ($viteConfig -notmatch "port:\s*$script:GenSWWebHttpsPort") {
        throw 'vite.config.ts não corresponde à porta HTTPS fixa do frontend.'
    }
}

function Assert-GenSWSensitiveArtifactsIgnored {
    Push-Location -LiteralPath $script:GenSWRoot
    try {
        $trackedSensitiveFiles = @(@(& git ls-files) | Where-Object {
            $_ -match '(^|/)\.env\.local$' -or
            $_ -match '(^|/)\.gensw/' -or
            $_ -match '\.(pem|key|pfx|p12)$'
        })
        if ($trackedSensitiveFiles.Count -gt 0) {
            throw 'Há arquivo local sensível rastreado pelo Git. Remova-o do índice antes de continuar.'
        }

        & git check-ignore --quiet -- '.gensw/run/processes.json'
        if ($LASTEXITCODE -ne 0) {
            throw 'O diretório runtime .gensw não está ignorado pelo Git.'
        }

        $relativeEnv = 'src/Frontend/GenSW.Web/.env.local'
        & git check-ignore --quiet -- $relativeEnv
        if ($LASTEXITCODE -ne 0) {
            throw 'O arquivo frontend .env.local não está ignorado pelo Git.'
        }

        $privateKeyFiles = @(& git grep -l -E 'BEGIN (RSA |EC )?PRIVATE KEY' -- 2>$null)
        if ($privateKeyFiles.Count -gt 0) {
            throw 'Foi encontrado conteúdo de chave privada em arquivo rastreado pelo Git.'
        }
    }
    finally {
        Pop-Location
    }
}

function Assert-GenSWLocalSetup {
    Assert-GenSWPrerequisites
    Assert-GenSWFixedConfiguration
    Assert-GenSWCertificate
    Assert-GenSWRequiredSecrets
    Assert-GenSWFrontendEnvLocal
    Assert-GenSWSensitiveArtifactsIgnored

    $nodeModules = Join-Path $script:GenSWFrontendDirectory 'node_modules'
    if (-not (Test-Path -LiteralPath $nodeModules -PathType Container)) {
        throw 'As dependências frontend não estão instaladas. Execute setup-gensw.bat primeiro.'
    }
}

function Get-GenSWPortOwner {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listener = Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -eq $listener) {
        return $null
    }

    $process = Get-Process -Id $listener.OwningProcess -ErrorAction SilentlyContinue
    $processName = '<desconhecido>'
    if ($null -ne $process) {
        $processName = $process.ProcessName
    }

    [pscustomobject]@{
        Port = $Port
        ProcessId = $listener.OwningProcess
        ProcessName = $processName
    }
}

function Assert-GenSWApplicationPortsAvailable {
    $checks = @(
        [pscustomobject]@{ Label = 'HTTPS do frontend'; Port = $script:GenSWWebHttpsPort },
        [pscustomobject]@{ Label = 'HTTPS da API'; Port = $script:GenSWApiHttpsPort },
        [pscustomobject]@{ Label = 'HTTP da API'; Port = $script:GenSWApiHttpPort }
    )

    foreach ($check in $checks) {
        $owner = Get-GenSWPortOwner -Port $check.Port
        if ($null -ne $owner) {
            $message = @"
GenSW não foi iniciado.

A porta $($check.Label) $($check.Port) já está em uso.
PID: $($owner.ProcessId)
Processo: $($owner.ProcessName)

Encerre o processo conflitante e execute start-gensw.bat novamente.
"@
            throw $message.Trim()
        }
    }
}

function Read-GenSWRuntimeState {
    if (-not (Test-Path -LiteralPath $script:GenSWRuntimeStateFile -PathType Leaf)) {
        return @()
    }

    try {
        $parsed = Get-Content -Raw -LiteralPath $script:GenSWRuntimeStateFile | ConvertFrom-Json
        return @($parsed)
    }
    catch {
        Write-GenSWWarning 'O estado runtime anterior é inválido e será descartado sem encerrar processos.'
        Remove-GenSWRuntimeState
        return @()
    }
}

function Save-GenSWRuntimeState {
    param([Parameter(Mandatory = $true)][object[]]$Records)

    New-Item -ItemType Directory -Path $script:GenSWRuntimeDirectory -Force | Out-Null
    $json = ConvertTo-Json -InputObject @($Records) -Depth 5
    [System.IO.File]::WriteAllText(
        $script:GenSWRuntimeStateFile,
        $json,
        (New-Object System.Text.UTF8Encoding($false)))
}

function Remove-GenSWRuntimeState {
    Remove-Item -LiteralPath $script:GenSWRuntimeStateFile -Force -ErrorAction SilentlyContinue
}

function Test-GenSWTrackedProcess {
    param([Parameter(Mandatory = $true)][object]$Record)

    if ($null -eq $Record.ProcessId -or $null -eq $Record.StartedAtUtc -or $null -eq $Record.Kind) {
        return $false
    }

    $process = Get-Process -Id ([int]$Record.ProcessId) -ErrorAction SilentlyContinue
    if ($null -eq $process) {
        return $false
    }

    if ($process.ProcessName -ne 'powershell' -or
        $process.ProcessName -ne [string]$Record.ExecutableName) {
        return $false
    }

    try {
        $recordedStart = [datetime]::Parse([string]$Record.StartedAtUtc).ToUniversalTime()
        $actualStart = $process.StartTime.ToUniversalTime()
        if ([Math]::Abs(($actualStart - $recordedStart).TotalSeconds) -gt 2) {
            return $false
        }

        $cimProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($Record.ProcessId)" -ErrorAction Stop
        $commandLine = [string]$cimProcess.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            return $false
        }

        $hasNoProfile = $commandLine.IndexOf('-NoProfile', [StringComparison]::OrdinalIgnoreCase) -ge 0
        $hasFileArgument = $commandLine.IndexOf('-File', [StringComparison]::OrdinalIgnoreCase) -ge 0
        $hasScript = $commandLine.IndexOf($script:GenSWStartScript, [StringComparison]::OrdinalIgnoreCase) -ge 0
        $marker = "-RunService $($Record.Kind)"
        $hasMarker = $commandLine.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0
        return $hasNoProfile -and $hasFileArgument -and $hasScript -and $hasMarker
    }
    catch {
        return $false
    }
}

function New-GenSWProcessRecord {
    param(
        [Parameter(Mandatory = $true)][Diagnostics.Process]$Process,
        [Parameter(Mandatory = $true)][ValidateSet('Api', 'Web')][string]$Kind
    )

    $Process.Refresh()
    [pscustomobject]@{
        Kind = $Kind
        ProcessId = $Process.Id
        StartedAtUtc = $Process.StartTime.ToUniversalTime().ToString('o')
        ExecutableName = $Process.ProcessName
    }
}

function Start-GenSWServiceWindow {
    param([Parameter(Mandatory = $true)][ValidateSet('Api', 'Web')][string]$Kind)

    $powershell = (Get-Command 'powershell.exe' -ErrorAction Stop).Source
    $arguments = @(
        '-NoProfile',
        '-ExecutionPolicy', 'Bypass',
        '-File', ('"{0}"' -f $script:GenSWStartScript),
        '-RunService', $Kind
    )

    $process = Start-Process -FilePath $powershell -ArgumentList $arguments -WorkingDirectory $script:GenSWRoot -PassThru
    Start-Sleep -Milliseconds 500
    return New-GenSWProcessRecord -Process $process -Kind $Kind
}

function Stop-GenSWProcessRecords {
    param([Parameter(Mandatory = $true)][object[]]$Records)

    $ordered = @($Records | Sort-Object @{ Expression = { if ($_.Kind -eq 'Web') { 0 } else { 1 } } })
    foreach ($record in $ordered) {
        if (-not (Test-GenSWTrackedProcess -Record $record)) {
            Write-GenSWWarning "PID $($record.ProcessId) ($($record.Kind)) não corresponde mais à instância registrada; nenhum processo foi encerrado."
            continue
        }

        Write-GenSWInfo "Encerrando GenSW $($record.Kind) (PID $($record.ProcessId))..."
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            & taskkill.exe /PID ([string]$record.ProcessId) /T /F 2>$null | Out-Null
            $taskKillExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        if ($taskKillExitCode -ne 0 -and (Get-Process -Id ([int]$record.ProcessId) -ErrorAction SilentlyContinue)) {
            throw "Não foi possível encerrar o processo GenSW $($record.Kind), PID $($record.ProcessId)."
        }
    }
}

function Get-GenSWValidatedRuntimeState {
    $records = @(Read-GenSWRuntimeState)
    $valid = @($records | Where-Object { Test-GenSWTrackedProcess -Record $_ })

    [pscustomobject]@{
        All = $records
        Valid = $valid
        HasApi = @($valid | Where-Object Kind -eq 'Api').Count -eq 1
        HasWeb = @($valid | Where-Object Kind -eq 'Web').Count -eq 1
    }
}

function Wait-GenSWHttpReadiness {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][object]$ProcessRecord,
        [int]$TimeoutSeconds = 120
    )

    $deadline = [datetime]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([datetime]::UtcNow -lt $deadline) {
        if (-not (Test-GenSWTrackedProcess -Record $ProcessRecord)) {
            throw "$Name encerrou antes de ficar disponível. Verifique a janela de logs correspondente."
        }

        try {
            $response = Invoke-WebRequest -Uri $Uri -UseBasicParsing -TimeoutSec 5
            if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
                Write-GenSWInfo "$Name disponível."
                return
            }
        }
        catch {
            # O serviço ainda pode estar inicializando.
        }

        Start-Sleep -Seconds 1
    }

    throw "Timeout de $TimeoutSeconds segundos aguardando $Name em $Uri."
}

function Write-GenSWStartedSummary {
    Write-Host ''
    Write-Host 'GenSW iniciado.' -ForegroundColor Green
    Write-Host ''
    Write-Host 'Web:'
    Write-Host $script:GenSWWebUrl
    Write-Host ''
    Write-Host 'Login:'
    Write-Host $script:GenSWLoginUrl
    Write-Host ''
    Write-Host 'API:'
    Write-Host $script:GenSWApiUrl
    Write-Host ''
    Write-Host 'Swagger:'
    Write-Host $script:GenSWSwaggerUrl
    Write-Host ''
    Write-Host 'PostgreSQL:'
    Write-Host "localhost:$script:GenSWPostgreSqlPort"
    Write-Host ''
    Write-Host 'Use stop-gensw.bat para encerrar o GenSW.'
}
