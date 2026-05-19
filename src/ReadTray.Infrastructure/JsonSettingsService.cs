using System.Text.Json;
using System.IO;
using ReadTray.Core;

namespace ReadTray.Infrastructure;

public sealed class JsonSettingsService : ISettingsService
{
    private readonly ISecretStore _secretStore;
    private readonly string _path;

    public JsonSettingsService(ISecretStore secretStore)
    {
        _secretStore = secretStore;
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReadTray");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, "settings.json");
    }

    public async Task<AppSettings> LoadAsync(CancellationToken ct)
    {
        AppSettings settings;
        if (!File.Exists(_path))
        {
            settings = new AppSettings();
        }
        else
        {
            await using var stream = File.OpenRead(_path);
            settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, cancellationToken: ct) ?? new AppSettings();
        }

        if (settings.ReadSelectedHotkey == new HotkeyGesture(true, true, false, false, "Space"))
        {
            settings.ReadSelectedHotkey = new AppSettings().ReadSelectedHotkey;
        }

        if (settings.ReadSelectedHotkey == new HotkeyGesture(true, false, true, true, "Space"))
        {
            settings.ReadSelectedHotkey = new AppSettings().ReadSelectedHotkey;
        }

        if (settings.ReadSelectedHotkey == new HotkeyGesture(true, false, true, true, "R"))
        {
            settings.ReadSelectedHotkey = new AppSettings().ReadSelectedHotkey;
        }

        if (settings.ReadSelectedHotkey == new HotkeyGesture(true, true, true, false, "F12"))
        {
            settings.ReadSelectedHotkey = new AppSettings().ReadSelectedHotkey;
        }

        settings.ElevenLabsApiKey = _secretStore.GetSecret("elevenlabs-api-key");
        return settings;
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken ct)
    {
        _secretStore.SetSecret("elevenlabs-api-key", settings.ElevenLabsApiKey);
        var persisted = new AppSettings
        {
            ReadSelectedHotkey = settings.ReadSelectedHotkey,
            ReadClipboardHotkey = settings.ReadClipboardHotkey,
            SelectedProviderId = settings.SelectedProviderId,
            SelectedVoiceByProvider = settings.SelectedVoiceByProvider,
            Speed = settings.Speed,
            AutoStartWithWindows = settings.AutoStartWithWindows,
            AutoHidePlayer = settings.AutoHidePlayer,
            AutoHideSeconds = settings.AutoHideSeconds,
            DuckOtherAudio = settings.DuckOtherAudio,
            DuckOtherAudioVolumePercent = settings.DuckOtherAudioVolumePercent,
            CheckForUpdatesOnStartup = settings.CheckForUpdatesOnStartup,
            PopupPosition = settings.PopupPosition,
            CaptureStrategy = settings.CaptureStrategy,
            RestoreClipboardText = settings.RestoreClipboardText,
            PrivacyMode = settings.PrivacyMode,
            DebugLoggingEnabled = settings.DebugLoggingEnabled,
            DebugLogTextPreview = settings.DebugLogTextPreview,
            ElevenLabsModelId = settings.ElevenLabsModelId,
            ElevenLabsCustomVoiceId = settings.ElevenLabsCustomVoiceId
        };

        await using var stream = File.Create(_path);
        await JsonSerializer.SerializeAsync(stream, persisted, new JsonSerializerOptions { WriteIndented = true }, ct);
    }
}
