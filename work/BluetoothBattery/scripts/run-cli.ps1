param(
    [string]$JsonPath,
    [string]$ConfigPath,
    [switch]$Raw,
    [switch]$Deep,
    [switch]$All,
    [switch]$ConnectedOnly,
    [switch]$Summary,
    [switch]$ProviderDiagnostics,
    [switch]$WriteSampleConfig,
    [switch]$Help,
    [string]$BleBatteryAddress,
    [switch]$RazerViperV2Battery,
    [int]$TimeoutSeconds = 30,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$ExtraArgs
)

$ErrorActionPreference = 'Stop'

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$dotnet = Join-Path $workspaceRoot 'work\.dotnet\dotnet.exe'
$project = Join-Path $workspaceRoot 'work\BluetoothBattery\src\BluetoothBattery.Cli\BluetoothBattery.Cli.csproj'

$env:DOTNET_CLI_HOME = Join-Path $workspaceRoot 'work\.dotnet-home'
$env:NUGET_PACKAGES = Join-Path $workspaceRoot 'work\.nuget\packages'
$env:APPDATA = Join-Path $workspaceRoot 'work\.appdata\roaming'
$env:LOCALAPPDATA = Join-Path $workspaceRoot 'work\.appdata\local'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'

$arguments = @('run', '--project', $project, '--')
if ($Help -or ($ExtraArgs -contains '--help') -or ($ExtraArgs -contains '-h')) {
    $arguments += '--help'
}

if ($Raw) {
    $arguments += '--raw'
}

if ($Summary) {
    $arguments += '--summary'
}

if ($WriteSampleConfig) {
    $arguments += '--write-sample-config'
}

if ($Deep) {
    $arguments += '--deep'
}

if ($All) {
    $arguments += '--all'
}

if ($ConnectedOnly) {
    $arguments += '--connected-only'
}

if ($ProviderDiagnostics) {
    $arguments += '--provider-diagnostics'
}

if ($TimeoutSeconds -gt 0) {
    $arguments += '--timeout-seconds'
    $arguments += [string]$TimeoutSeconds
}

if ($JsonPath) {
    $arguments += '--json'
    $arguments += $JsonPath
}

if ($ConfigPath) {
    $arguments += '--config'
    $arguments += $ConfigPath
}

if ($BleBatteryAddress) {
    $arguments += '--ble-battery'
    $arguments += $BleBatteryAddress
}

if ($RazerViperV2Battery) {
    $arguments += '--razer-viper-v2-battery'
}

& $dotnet @arguments
exit $LASTEXITCODE
