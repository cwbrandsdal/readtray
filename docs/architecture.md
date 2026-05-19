# ReadTray Architecture Note

## WinUI 3 vs WPF

ReadTray needs to be a hidden tray utility with reliable global hotkeys, clipboard interop, low-friction Win32 calls, always-on-top floating UI, and fast delivery.

WinUI 3 has the newer Windows 11 control style and a modern app model, but tray apps and global hotkeys still require dropping down to Win32. It also adds packaging, windowing, and startup complexity that does not help the MVP.

WPF is older, but it is the pragmatic fit for this version. It works well with `NotifyIcon`, Win32 `RegisterHotKey`, clipboard APIs, transparent floating windows, and `System.Speech` local TTS. It can still look modern with custom styling and runs on Windows 10/11 without requiring admin rights.

Decision: build the MVP in WPF on .NET 10 Windows. Keep provider, capture, playback, settings, and tray logic isolated so a future WinUI 3 shell can reuse most of the non-UI code.

## MVP Shape

- `ReadTray.App`: WPF shell, tray integration, global hotkeys, floating player, settings window.
- `ReadTray.Core`: contracts, models, text cleaning.
- `ReadTray.Infrastructure`: clipboard capture, settings persistence, DPAPI secret storage, streamed playback service.
- `ReadTray.Tts.ElevenLabs`: streaming HTTP provider.
- `ReadTray.Tts.Windows`: local Windows voice fallback.
- `ReadTray.Tests`: focused unit tests around pure logic and settings models.

Known MVP limitation: cloud audio playback currently buffers streamed chunks to a temporary audio file before playing through WPF `MediaPlayer`. The provider streams over HTTP and cancellation works, but true chunk-by-chunk MP3 playback should be upgraded with a dedicated audio pipeline. Windows local TTS starts immediately through `System.Speech`.
