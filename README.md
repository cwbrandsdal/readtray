# ReadTray

ReadTray is a native Windows tray app for fast selected-text-to-speech. Highlight text in any Windows app, press `Ctrl+Shift+F12`, and ReadTray captures the selection, opens a compact floating player, and reads it aloud.

## Architecture Choice

The MVP uses WPF on .NET 10 Windows. WinUI 3 is attractive for modern Windows styling, but WPF is the more reliable, lower-friction choice for a tray-first utility with Win32 global hotkeys, clipboard interop, transparent floating windows, and Windows local TTS. See [docs/architecture.md](docs/architecture.md).

## Requirements

- Windows 10 or Windows 11
- .NET SDK available on the machine. This repo currently targets `net10.0-windows` because the development machine has .NET `10.0.300-preview` installed.

## Run

```powershell
dotnet run --project src\ReadTray.App\ReadTray.App.csproj
```

The app starts hidden in the system tray. Use the tray menu to read selected text, read clipboard text, stop playback, open settings, or quit.

## Default Hotkeys

- `Ctrl+Shift+F12`: read selected text
- `Ctrl+Alt+Shift+Space`: read clipboard directly

If a hotkey cannot be registered, ReadTray shows a warning. Close the conflicting app or edit `%APPDATA%\ReadTray\settings.json`.

## Configure ElevenLabs

1. Open the tray menu.
2. Choose `Settings`.
3. Paste your ElevenLabs API key.
4. Select `ElevenLabs`.
5. Click `Refresh voices`.
6. Select a voice and save.

API keys are stored separately using DPAPI under the current Windows user profile. They are not written to the normal settings file and are not logged.

## OpenAI TTS

OpenAI is intentionally isolated for a later provider project. The app architecture already supports adding it without changing the UI flow.

## Local Windows TTS

The `Windows` provider uses installed Windows voices and requires no API key. Enable privacy mode in Settings to force local-only speech.

## Publish

```powershell
.\scripts\publish.ps1
```

Output is written to `artifacts\publish\ReadTray`. The script publishes a self-contained Windows x64 build.

To also create a Start Menu shortcut for the current user:

```powershell
.\scripts\publish.ps1 -CreateStartMenuShortcut
```

Windows Start Menu shortcuts use the embedded `.ico` from the executable. The source PNG is included under `Assets\ReadTray.png` for future installer/MSIX packaging.

## Updates

ReadTray can check GitHub Releases from the tray menu with `Check for updates`. It can also check on startup when enabled in Settings.

The repository includes a GitHub Actions release workflow that publishes Velopack installer/update assets to GitHub Releases on pushes to `main`. Installed builds can download and apply updates from the tray menu. See [docs/update-strategy.md](docs/update-strategy.md).

## Installer

Velopack is the recommended installer path for ReadTray because it is closest to Electron-style GitHub Releases updates. Create a local installer package with:

```powershell
.\scripts\package-velopack.ps1 -Version 0.1.0
```

See [docs/installer-options.md](docs/installer-options.md) for Velopack vs MSIX tradeoffs.

## Known Limitations

- MVP text capture uses clipboard copy first. It restores previous clipboard text when possible, but complex non-text formats are not preserved yet.
- UI Automation selected-text capture is planned after MVP.
- ElevenLabs audio is requested with streaming HTTP, but playback currently buffers the streamed MP3 chunks to a temporary file before starting playback. Windows local TTS starts immediately.
- Hotkey editing has a settings model, but the current UI only documents the defaults. Manual edits can be made in `%APPDATA%\ReadTray\settings.json`.
- Debug logging is off by default. Enable it from Settings when diagnosing an issue, then restart the app.

## Troubleshooting

### Hotkey Conflicts

If `Ctrl+Shift+F12` does not work, another app may already own the shortcut. ReadTray logs the registration failure and shows a friendly warning on startup.

### Clipboard Capture

Some apps block simulated `Ctrl+C` or delay clipboard updates. Use the tray `Read clipboard` command or the manual text box fallback if no selected text is detected.

### Logs

Logs are written to:

```text
%LOCALAPPDATA%\ReadTray\logs
```

ReadTray logs capture strategy, text length, provider selection, playback start/stop, and errors. It does not log full captured text by default.

## Public Release Hygiene

Read [SECURITY.md](SECURITY.md) and [docs/public-release-checklist.md](docs/public-release-checklist.md) before publishing. API keys are stored with DPAPI in `%APPDATA%\ReadTray\secrets.json`; local settings, secrets, logs, certificates, publish profiles, and build artifacts should never be committed.
