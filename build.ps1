# Builds BWU (self-contained, win-x64) and, if NSIS is installed, the installer.
# Run from the repository root in PowerShell:  .\build.ps1
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

Write-Host "SDK in use:" (dotnet --version)   # global.json pins the 8.0.x SDK on purpose
dotnet restore "$root\BWU.sln"
dotnet build   "$root\BWU.sln" -c Release --no-restore
dotnet publish "$root\BWU\BWU.csproj" -c Release -r win-x64 --self-contained true -o "$root\publish"

$makensis = Get-Command makensis -ErrorAction SilentlyContinue
if (-not $makensis) {
    $default = "${env:ProgramFiles(x86)}\NSIS\makensis.exe"
    if (Test-Path $default) { $makensis = Get-Item $default }
}
if ($makensis) {
    New-Item -ItemType Directory -Force "$root\dist" | Out-Null
    & $makensis.Source "$root\installer\bwu.nsi"
    Write-Host "Installer created in $root\dist"
} else {
    Write-Warning "NSIS (makensis) was not found. Install it from https://nsis.sourceforge.io and run this script again to build the installer."
}
