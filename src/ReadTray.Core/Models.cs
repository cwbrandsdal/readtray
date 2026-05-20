namespace ReadTray.Core;

public sealed record TextCaptureResult(string? Text, TextCaptureStrategy Strategy, bool Success, string? Message = null);

public enum TextCaptureStrategy
{
    Clipboard,
    UiAutomation,
    Manual
}

public sealed record AudioChunk(byte[] Data, string ContentType);

public sealed record TtsVoice(string Id, string DisplayName, string? Locale = null);

public sealed record TtsModel(string Id, string DisplayName);

public sealed record TtsRequest(string Text, string VoiceId, double Speed, string? ModelId = null);

public sealed record PlaybackOptions(string? ContentType = null, double Speed = 1.0);

public sealed class AppSettings
{
    public HotkeyGesture ReadSelectedHotkey { get; set; } = new(true, false, true, false, "F12");
    public HotkeyGesture ReadClipboardHotkey { get; set; } = new(true, true, true, false, "Space");
    public string SelectedProviderId { get; set; } = "windows";
    public Dictionary<string, string> SelectedVoiceByProvider { get; set; } = new();
    public double Speed { get; set; } = 1.0;
    public bool AutoStartWithWindows { get; set; }
    public bool AutoHidePlayer { get; set; } = true;
    public int AutoHideSeconds { get; set; } = 4;
    public bool DuckOtherAudio { get; set; }
    public double DuckOtherAudioVolumePercent { get; set; } = 25;
    public bool CheckForUpdatesOnStartup { get; set; } = true;
    public string PopupPosition { get; set; } = "Cursor";
    public string CaptureStrategy { get; set; } = "Clipboard";
    public bool RestoreClipboardText { get; set; } = true;
    public bool PrivacyMode { get; set; }
    public bool DebugLoggingEnabled { get; set; }
    public bool DebugLogTextPreview { get; set; }
    public string ElevenLabsModelId { get; set; } = "eleven_turbo_v2_5";
    public string? ElevenLabsCustomVoiceId { get; set; }
    public string? ElevenLabsApiKey { get; set; }
}

public sealed record HotkeyGesture(bool Control, bool Alt, bool Shift, bool Win, string Key)
{
    public override string ToString()
    {
        var parts = new List<string>();
        if (Control) parts.Add("Ctrl");
        if (Alt) parts.Add("Alt");
        if (Shift) parts.Add("Shift");
        if (Win) parts.Add("Win");
        parts.Add(Key);
        return string.Join("+", parts);
    }
}

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string CurrentVersion,
    string? LatestVersion,
    string? ReleaseUrl,
    string? AssetUrl,
    string? AssetName,
    string? Message);
