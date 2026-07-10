$ErrorActionPreference = 'Stop'

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$dotnet = Join-Path $workspaceRoot 'work\.dotnet\dotnet.exe'
$project = Join-Path $workspaceRoot 'work\BluetoothBattery\src\BluetoothBattery.App\BluetoothBattery.App.csproj'
$publishDir = Join-Path $workspaceRoot 'outputs\BluetoothBattery.App'
$nugetConfig = Join-Path $workspaceRoot 'NuGet.Config'

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot 'work\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspaceRoot 'work\.nuget\packages'
$env:APPDATA = Join-Path $workspaceRoot 'work\.appdata\roaming'
$env:LOCALAPPDATA = Join-Path $workspaceRoot 'work\.appdata\local'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained false `
    --configfile $nugetConfig `
    -p:WindowsPackageType=None `
    -p:EnableMsixTooling=false `
    -p:GenerateAppxPackageOnBuild=false `
    -p:PublishSingleFile=false `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=false `
    -o $publishDir

if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$exe = Join-Path $publishDir 'BluetoothBattery.App.exe'
Write-Host "Published to: $publishDir"
Write-Host "Run: $exe"
