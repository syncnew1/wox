$ErrorActionPreference = 'Stop'

$workspaceRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..\..')
$bundledGit = 'C:\Users\zxq_1\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
$git = if (Test-Path $bundledGit) { $bundledGit } else { 'git' }

Push-Location $workspaceRoot
try {
    & $git rm -r --cached --ignore-unmatch `
        work\.dotnet `
        work\.dotnet-home `
        work\.nuget `
        work\.appdata `
        outputs `
        work\dotnet-install.ps1 `
        work\BluetoothBattery\src\BluetoothBattery.Core\bin `
        work\BluetoothBattery\src\BluetoothBattery.Core\obj `
        work\BluetoothBattery\src\BluetoothBattery.Cli\bin `
        work\BluetoothBattery\src\BluetoothBattery.Cli\obj `
        work\BluetoothBattery\src\BluetoothBattery.App\bin `
        work\BluetoothBattery\src\BluetoothBattery.App\obj

    & $git add .gitignore BluetoothBattery.sln NuGet.Config work\BluetoothBattery
    & $git status --short --branch
}
finally {
    Pop-Location
}
