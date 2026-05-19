using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTray.Core;
using ReadTray.Tts.ElevenLabs;

namespace ReadTray.App.Services;

public sealed class ReadCoordinator
{
    private readonly ITextCaptureService _captureService;
    private readonly ISettingsService _settingsService;
    private readonly IEnumerable<ITtsProvider> _providers;
    private readonly ISpeechPlaybackService _playbackService;
    private readonly IServiceProvider _services;
    private readonly ILogger<ReadCoordinator> _logger;
    private CancellationTokenSource? _currentRead;
    private FloatingPlayerWindow? _player;
    private string _lastText = string.Empty;

    public ReadCoordinator(ITextCaptureService captureService, ISettingsService settingsService, IEnumerable<ITtsProvider> providers, ISpeechPlaybackService playbackService, IServiceProvider services, ILogger<ReadCoordinator> logger)
    {
        _captureService = captureService;
        _settingsService = settingsService;
        _providers = providers;
        _playbackService = playbackService;
        _services = services;
        _logger = logger;
    }

    public Task ReadSelectedAsync() => CaptureAndReadAsync(false);
    public Task ReadClipboardAsync() => CaptureAndReadAsync(true);
    public void Stop() { _currentRead?.Cancel(); _playbackService.Stop(); _player?.SetStatus("Stopped"); }
    public void Pause() => _playbackService.Pause();
    public void Resume() => _playbackService.Resume();
    public void SetSpeed(double speed) => _playbackService.SetSpeed(speed);
    public Task RestartAsync() => string.IsNullOrWhiteSpace(_lastText) ? Task.CompletedTask : ReadManualTextAsync(_lastText);

    public async Task ReadManualTextAsync(string text)
    {
        _currentRead?.Cancel();
        _playbackService.Stop();
        _currentRead = new CancellationTokenSource();
        await ReadTextAsync(text, _currentRead.Token);
    }

    private async Task CaptureAndReadAsync(bool clipboardOnly)
    {
        try
        {
            Stop();
            _currentRead = new CancellationTokenSource();
            var ct = _currentRead.Token;
            _logger.LogInformation("Hotkey triggered. ClipboardOnly: {ClipboardOnly}", clipboardOnly);
            var result = clipboardOnly ? await _captureService.CaptureClipboardTextAsync(ct) : await _captureService.CaptureSelectedTextAsync(ct);
            ShowPlayer(result.Text ?? string.Empty, result.Message, !result.Success);
            if (result.Success && !string.IsNullOrWhiteSpace(result.Text))
            {
                await ReadTextAsync(result.Text, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Read flow failed.");
            ShowPlayer(string.Empty, UserFacingError(ex), true);
        }
    }

    private async Task ReadTextAsync(string text, CancellationToken ct)
    {
        _lastText = text;
        var settings = await _settingsService.LoadAsync(ct);
        var providerId = settings.PrivacyMode ? "windows" : settings.SelectedProviderId;
        var provider = _providers.FirstOrDefault(p => p.Id == providerId) ?? _providers.First(p => p.Id == "windows");
        var voice = settings.SelectedVoiceByProvider.GetValueOrDefault(provider.Id) ?? string.Empty;
        _logger.LogInformation("Provider selected: {Provider}. Text length: {Length}", provider.Id, text.Length);
        _player?.SetStatus("Reading");
        var request = new TtsRequest(text, voice, settings.Speed, settings.ElevenLabsModelId);
        try
        {
            if (provider is ILocalSpeechProvider local)
            {
                await _playbackService.PlayLocalAsync(local, request, ct);
            }
            else
            {
                await _playbackService.PlayAsync(provider.StreamSpeechAsync(request, ct), new PlaybackOptions("audio/mpeg", settings.Speed), ct);
            }

            _logger.LogInformation("Playback completed.");
            _player?.SetStatus("Done");
            if (settings.AutoHidePlayer)
            {
                _player?.HideAfter(TimeSpan.FromSeconds(settings.AutoHideSeconds));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Playback failed.");
            _player?.SetStatus(UserFacingError(ex));
        }
    }

    private void ShowPlayer(string text, string? status, bool manual)
    {
        _player ??= _services.GetRequiredService<FloatingPlayerWindow>();
        _player.SetContent(text, status, manual);
        _player.ShowNearCursor();
    }

    private static string UserFacingError(Exception ex)
    {
        return ex is ElevenLabsApiException apiException
            ? $"ElevenLabs {(int)apiException.StatusCode}: {apiException.ProviderMessage ?? apiException.ProviderCode ?? "See log file."}"
            : $"{ex.GetType().Name}. See log file.";
    }
}
