namespace ReadTray.Core;

public interface ITextCaptureService
{
    Task<TextCaptureResult> CaptureSelectedTextAsync(CancellationToken ct);
    Task<TextCaptureResult> CaptureClipboardTextAsync(CancellationToken ct);
}

public interface ITextCleaningService
{
    string Clean(string text);
}

public interface ITtsProvider
{
    string Id { get; }
    string DisplayName { get; }
    Task<IReadOnlyList<TtsVoice>> GetVoicesAsync(CancellationToken ct);
    IAsyncEnumerable<AudioChunk> StreamSpeechAsync(TtsRequest request, CancellationToken ct);
}

public interface ILocalSpeechProvider : ITtsProvider
{
    Task SpeakAsync(TtsRequest request, CancellationToken ct);
    void Pause();
    void Resume();
    void Stop();
    void SetSpeed(double speed);
}

public interface ISpeechPlaybackService
{
    Task PlayAsync(IAsyncEnumerable<AudioChunk> chunks, PlaybackOptions options, CancellationToken ct);
    Task PlayLocalAsync(ILocalSpeechProvider provider, TtsRequest request, CancellationToken ct);
    void Pause();
    void Resume();
    void Stop();
    void SetSpeed(double speed);
}

public interface IAudioDuckingService
{
    Task BeginDuckingAsync(double volumePercent, CancellationToken ct);
    void EndDucking();
}

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckForUpdatesAsync(CancellationToken ct);
    Task<UpdateCheckResult> DownloadAndApplyLatestUpdateAsync(IProgress<int>? progress, CancellationToken ct);
}

public interface ISettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken ct);
    Task SaveAsync(AppSettings settings, CancellationToken ct);
}

public interface ISecretStore
{
    string? GetSecret(string name);
    void SetSecret(string name, string? value);
}
