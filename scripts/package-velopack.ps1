param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "0.1.0",
    [string]$Channel = "win"
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repo "artifacts\publish\ReadTray"
$releaseDir = Join-Path $repo "artifacts\velopack"

& (Join-Path $PSScriptRoot "publish.ps1") `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -Version $Version

if (Test-Path $releaseDir) {
    Remove-Item $releaseDir -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $releaseDir | Out-Null

dotnet tool restore
dotnet vpk pack `
    --packId "ReadTray" `
    --packTitle "ReadTray" `
    --packVersion $Version `
    --packDir $publishDir `
    --mainExe "ReadTray.App.exe" `
    --channel $Channel `
    --outputDir $releaseDir `
    --icon (Join-Path $repo "src\ReadTray.App\Assets\ReadTray.ico")

Write-Host "Created Velopack installer/release package in $releaseDir"
