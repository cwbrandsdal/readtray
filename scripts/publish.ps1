param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$Version = "",
    [switch]$CreateStartMenuShortcut
)

$ErrorActionPreference = "Stop"
$repo = Split-Path -Parent $PSScriptRoot
$output = Join-Path $repo "artifacts\publish\ReadTray"

$publishArgs = @(
    "publish",
    (Join-Path $repo "src\ReadTray.App\ReadTray.App.csproj"),
    "-c", $Configuration,
    "-r", $Runtime,
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-o", $output
)

if (-not [string]::IsNullOrWhiteSpace($Version)) {
    $publishArgs += "-p:Version=$Version"
    $publishArgs += "-p:AssemblyVersion=$Version.0"
    $publishArgs += "-p:FileVersion=$Version.0"
}

dotnet @publishArgs

$exe = Join-Path $output "ReadTray.App.exe"
if ($CreateStartMenuShortcut) {
    $programs = [Environment]::GetFolderPath("Programs")
    $shortcutPath = Join-Path $programs "ReadTray.lnk"
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $exe
    $shortcut.WorkingDirectory = $output
    $shortcut.Description = "ReadTray selected-text-to-speech"
    $shortcut.IconLocation = "$exe,0"
    $shortcut.Save()
    Write-Host "Created Start Menu shortcut at $shortcutPath"
}

Write-Host "Published ReadTray to $output"
