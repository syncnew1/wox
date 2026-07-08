$ErrorActionPreference = 'Stop'

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$dotnet = Join-Path $workspaceRoot 'work\.dotnet\dotnet.exe'

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot 'work\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspaceRoot 'work\.nuget\packages'
$env:APPDATA = Join-Path $workspaceRoot 'work\.appdata\roaming'
$env:LOCALAPPDATA = Join-Path $workspaceRoot 'work\.appdata\local'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

& $dotnet new install Microsoft.WindowsAppSDK.WinUI.CSharp.Templates
& $dotnet new list winui
