$ErrorActionPreference = 'Stop'

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$dotnet = Join-Path $workspaceRoot 'work\.dotnet\dotnet.exe'
$coreProject = Join-Path $workspaceRoot 'work\BluetoothBattery\src\BluetoothBattery.Core\BluetoothBattery.Core.csproj'
$cliProject = Join-Path $workspaceRoot 'work\BluetoothBattery\src\BluetoothBattery.Cli\BluetoothBattery.Cli.csproj'
$nugetConfig = Join-Path $workspaceRoot 'NuGet.Config'

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot 'work\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspaceRoot 'work\.nuget\packages'
$env:APPDATA = Join-Path $workspaceRoot 'work\.appdata\roaming'
$env:LOCALAPPDATA = Join-Path $workspaceRoot 'work\.appdata\local'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

& $dotnet restore $coreProject --configfile $nugetConfig
& $dotnet restore $cliProject --configfile $nugetConfig
& $dotnet build $coreProject --no-restore -v minimal
& $dotnet build $cliProject --no-restore -v minimal
