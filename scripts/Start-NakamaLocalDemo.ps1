[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidateSet('Api', 'Frontend')]
    [string] $Service
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$apiProject = Join-Path $workspace 'src/backend/Nakama.Api/Nakama.Api.csproj'

if ($Service -eq 'Frontend') {
    Set-Location -LiteralPath (Join-Path $workspace 'src/frontend')
    $version = [version]((& node.exe --version).TrimStart('v'))
    if (-not (($version.Major -eq 22 -and $version -ge [version]'22.22.2') -or $version -ge [version]'24.15.0')) {
        $pinnedVersion = (Get-Content -LiteralPath '.nvmrc' -Raw).Trim()
        $nodePath = & npm.cmd exec --yes "--package=node@$pinnedVersion" -- node -p process.execPath
        if ($LASTEXITCODE -ne 0) { throw 'No se pudo preparar la versión de Node indicada en .nvmrc.' }
        $env:Path = "$(Split-Path -Parent $nodePath)$([IO.Path]::PathSeparator)$env:Path"
    }
    if (-not (Test-Path -LiteralPath 'node_modules/vite/bin/vite.js')) {
        & npm.cmd ci
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    $env:VITE_API_BASE_URL = 'http://localhost:5510'
    & npm.cmd run dev -- --host localhost --port 5511 --strictPort
    exit $LASTEXITCODE
}

$localConnection = $env:ConnectionStrings__NakamaDemoDatabase
if (-not $localConnection) {
    $secretFile = Join-Path $env:APPDATA 'Microsoft/UserSecrets/nakama-api-local-development/secrets.json'
    if (Test-Path -LiteralPath $secretFile) {
        $configuration = Get-Content -LiteralPath $secretFile -Raw | ConvertFrom-Json -AsHashtable
        $localConnection = $configuration['ConnectionStrings:NakamaTestDatabase'] ?? $configuration['ConnectionStrings:NakamaDatabase']
    }
}
if (-not $localConnection) { throw 'Configura ConnectionStrings__NakamaDemoDatabase con PostgreSQL local. El usuario necesita permiso para crear bases.' }

$connection = [System.Data.Common.DbConnectionStringBuilder]::new()
$connection.set_ConnectionString($localConnection)
if ($connection.get_Item('Host') -notin @('localhost', '127.0.0.1', '::1')) {
    throw 'La demostración requiere un host PostgreSQL de loopback.'
}

$demoDirectory = Join-Path $env:LOCALAPPDATA 'Nakama/local-demo'
$accessFile = Join-Path $demoDirectory 'acceso-demo.json'
New-Item -ItemType Directory -Path $demoDirectory -Force | Out-Null
if (-not (Test-Path -LiteralPath $accessFile)) {
    $databaseSuffix = [guid]::NewGuid().ToString('N').Substring(0, 10)
    $fixture = [ordered]@{
        url = 'http://localhost:5511'
        database = "nakama_local_demo_$databaseSuffix"
        admin = @{ user = 'ana.torres@pilot.local'; password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) }
        collaborator = @{ user = 'diego.ramos@pilot.local'; password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) }
        outsider = @{ user = 'lucia.vargas@pilot.local'; password = [Convert]::ToHexString([Security.Cryptography.RandomNumberGenerator]::GetBytes(16)) }
    }
    $fixture | ConvertTo-Json | Set-Content -LiteralPath $accessFile -Encoding utf8
}
$demo = Get-Content -LiteralPath $accessFile -Raw | ConvertFrom-Json
if ($demo.database -cnotmatch '^nakama_local_demo_[a-f0-9]{10}$') { throw 'El archivo local no identifica una base aislada de demostración.' }

$connection.set_Item('Database', $demo.database)
$env:ConnectionStrings__NakamaDatabase = $connection.get_ConnectionString()
$env:DOTNET_ENVIRONMENT = 'Development'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:AllowedHosts = 'localhost'
$env:Cors__AllowedOrigins__0 = 'http://localhost:5511'
$env:Authentication__Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
$env:Attachments__Provider = 'Local'
$env:Attachments__StoragePath = Join-Path $demoDirectory $demo.database
$env:DevelopmentBootstrap__Admin__Email = ' '
$env:DevelopmentBootstrap__Admin__Password = ' '
$env:DevelopmentBootstrap__PilotData__Enabled = 'true'
$env:DevelopmentBootstrap__PilotData__AdoptExistingProject = 'false'
$env:DevelopmentBootstrap__PilotData__AdminPassword = $demo.admin.password
$env:DevelopmentBootstrap__PilotData__CollaboratorPassword = $demo.collaborator.password
$env:DevelopmentBootstrap__PilotData__NonMemberPassword = $demo.outsider.password
$env:Serilog__MinimumLevel__Override__Microsoft = 'Warning'
Remove-Item Env:PORT -ErrorAction SilentlyContinue

& dotnet ef database update --project $apiProject --startup-project $apiProject
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output "Cuentas sintéticas de localhost: $accessFile"
& dotnet run --no-launch-profile --project $apiProject -- --urls 'http://localhost:5510'
exit $LASTEXITCODE
