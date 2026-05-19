using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using ReadTray.Core;
using Clipboard = System.Windows.Clipboard;

namespace ReadTray.Infrastructure;

public sealed class ClipboardTextCaptureService : ITextCaptureService
{
    private readonly ISettingsService _settingsService;
    private readonly ITextCleaningService _cleaner;
    private readonly ILogger<ClipboardTextCaptureService> _logger;

    public ClipboardTextCaptureService(ISettingsService settingsService, ITextCleaningService cleaner, ILogger<ClipboardTextCaptureService> logger)
    {
        _settingsService = settingsService;
        _cleaner = cleaner;
        _logger = logger;
    }

    public async Task<TextCaptureResult> CaptureSelectedTextAsync(CancellationToken ct)
    {
        var settings = await _settingsService.LoadAsync(ct);
        string? previousText = null;
        var hadText = false;

        await InvokeOnStaAsync(() =>
        {
            try
            {
                hadText = Clipboard.ContainsText();
                previousText = hadText ? Clipboard.GetText() : null;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to snapshot clipboard text before capture.");
            }
        });

        await WaitForHotkeyReleaseAsync(ct);
        _logger.LogDebug("Sending Ctrl+C for clipboard capture.");
        SendKeys.SendWait("^c");
        await Task.Delay(120, ct);

        var captured = await ReadClipboardTextAsync();
        if (settings.RestoreClipboardText && hadText)
        {
            await InvokeOnStaAsync(() =>
            {
                try
                {
                    Clipboard.SetText(previousText ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unable to restore previous clipboard text.");
                }
            });
        }

        var clean = _cleaner.Clean(captured ?? string.Empty);
        LogCapturedText(settings, clean);
        if (string.IsNullOrWhiteSpace(clean))
        {
            _logger.LogInformation("No selected text detected by clipboard capture.");
            return new TextCaptureResult(null, TextCaptureStrategy.Manual, false, "No selected text detected. Paste or type text to read.");
        }

        _logger.LogInformation("Captured selected text through clipboard. Length: {Length}", clean.Length);
        return new TextCaptureResult(clean, TextCaptureStrategy.Clipboard, true);
    }

    public async Task<TextCaptureResult> CaptureClipboardTextAsync(CancellationToken ct)
    {
        var text = _cleaner.Clean(await ReadClipboardTextAsync() ?? string.Empty);
        var settings = await _settingsService.LoadAsync(ct);
        LogCapturedText(settings, text);
        return string.IsNullOrWhiteSpace(text)
            ? new TextCaptureResult(null, TextCaptureStrategy.Manual, false, "Clipboard does not contain text. Paste or type text to read.")
            : new TextCaptureResult(text, TextCaptureStrategy.Clipboard, true);
    }

    private static Task<string?> ReadClipboardTextAsync()
    {
        return InvokeOnStaAsync(() =>
        {
            try
            {
                return Clipboard.ContainsText() ? Clipboard.GetText() : null;
            }
            catch
            {
                return null;
            }
        });
    }

    private static async Task WaitForHotkeyReleaseAsync(CancellationToken ct)
    {
        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(600);
        while (DateTimeOffset.UtcNow < deadline && AnyTriggerKeyDown())
        {
            await Task.Delay(20, ct);
        }

        await Task.Delay(40, ct);
    }

    private static bool AnyTriggerKeyDown()
    {
        return IsKeyDown(0x10) || IsKeyDown(0x11) || IsKeyDown(0x12) || IsKeyDown(0x5B) || IsKeyDown(0x5C) || IsKeyDown(0x7B);
    }

    private static bool IsKeyDown(int virtualKey)
    {
        return (GetAsyncKeyState(virtualKey) & 0x8000) != 0;
    }

    private void LogCapturedText(AppSettings settings, string text)
    {
        if (!settings.DebugLogTextPreview)
        {
            _logger.LogDebug("Captured text debug. Length={Length}", text.Length);
            return;
        }

        var preview = text.Length <= 80 ? text : text[..80] + "...";
        preview = preview.Replace("\r", "\\r").Replace("\n", "\\n");
        _logger.LogDebug("Captured text debug. Length={Length} Preview=\"{Preview}\"", text.Length, preview);
    }

    private static Task InvokeOnStaAsync(Action action) => InvokeOnStaAsync(() =>
    {
        action();
        return true;
    });

    private static Task<T> InvokeOnStaAsync<T>(Func<T> func)
    {
        if (System.Windows.Application.Current?.Dispatcher.CheckAccess() == true)
        {
            return Task.FromResult(func());
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher
            ?? throw new InvalidOperationException("Clipboard capture requires a WPF application dispatcher.");
        return dispatcher.InvokeAsync(func).Task;
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
