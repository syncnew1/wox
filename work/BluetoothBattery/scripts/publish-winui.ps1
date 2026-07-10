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

$resolvedPublishParent = Resolve-Path (Split-Path -Parent $publishDir)
$expectedPublishRoot = Join-Path $workspaceRoot 'outputs'
if ($resolvedPublishParent.Path -ne $expectedPublishRoot) {
    throw "Unexpected publish directory: $publishDir"
}

if (Test-Path $publishDir) {
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $publishDir | Out-Null

$selfContained = $true

& $dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained $selfContained `
    --configfile $nugetConfig `
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
Write-Host "Mode: WPF self-contained win-x64"
