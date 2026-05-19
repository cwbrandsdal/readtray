using System.Windows.Media;
using System.IO;
using Microsoft.Extensions.Logging;
using ReadTray.Core;

namespace ReadTray.Infrastructure;

public sealed class SpeechPlaybackService : ISpeechPlaybackService
{
    private readonly ILogger<SpeechPlaybackService> _logger;
    private readonly ISettingsService _settingsService;
    private readonly IAudioDuckingService _audioDuckingService;
    private readonly object _gate = new();
    private MediaPlayer? _player;
    private CancellationTokenSource? _playbackCts;
    private ILocalSpeechProvider? _localProvider;
    private string? _tempFile;

    public SpeechPlaybackService(ILogger<SpeechPlaybackService> logger, ISettingsService settingsService, IAudioDuckingService audioDuckingService)
    {
        _logger = logger;
        _settingsService = settingsService;
        _audioDuckingService = audioDuckingService;
    }

    public async Task PlayAsync(IAsyncEnumerable<AudioChunk> chunks, PlaybackOptions options, CancellationToken ct)
    {
        Stop();
        _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _playbackCts.Token;
        await BeginAudioDuckingIfEnabledAsync(token);
        var ext = options.ContentType?.Contains("mpeg", StringComparison.OrdinalIgnoreCase) == true ? ".mp3" : ".audio";
        _tempFile = Path.Combine(Path.GetTempPath(), $"readtray-{Guid.NewGuid():N}{ext}");
        _logger.LogDebug("Buffering streamed audio to temp file. Path={TempFile} ContentType={ContentType}", _tempFile, options.ContentType);

        await using (var file = File.Create(_tempFile))
        {
            await foreach (var chunk in chunks.WithCancellation(token))
            {
                await file.WriteAsync(chunk.Data, token);
            }
        }

        token.ThrowIfCancellationRequested();
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
        {
            _player = new MediaPlayer();
            _player.MediaEnded += (_, _) =>
            {
                CleanupTempFile();
                completion.TrySetResult();
            };
            _player.MediaFailed += (_, args) =>
            {
                CleanupTempFile();
                completion.TrySetException(args.ErrorException);
            };
            _player.Open(new Uri(_tempFile));
            _player.SpeedRatio = Math.Clamp(options.Speed, 0.5, 2.0);
            _player.Play();
            _logger.LogInformation("Playback started for streamed provider audio.");
        });
        await using var registration = token.Register(() => completion.TrySetCanceled(token));
        try
        {
            await completion.Task;
        }
        finally
        {
            _audioDuckingService.EndDucking();
        }
    }

    public async Task PlayLocalAsync(ILocalSpeechProvider provider, TtsRequest request, CancellationToken ct)
    {
        Stop();
        _localProvider = provider;
        _playbackCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        await BeginAudioDuckingIfEnabledAsync(_playbackCts.Token);
        _logger.LogInformation("Playback started for local provider. Provider={Provider} Voice={Voice} Speed={Speed} TextLength={TextLength}", provider.Id, request.VoiceId, request.Speed, request.Text.Length);
        try
        {
            await provider.SpeakAsync(request, _playbackCts.Token);
        }
        finally
        {
            _audioDuckingService.EndDucking();
        }
    }

    public void Pause()
    {
        lock (_gate)
        {
            _localProvider?.Pause();
            _player?.Pause();
        }
    }

    public void Resume()
    {
        lock (_gate)
        {
            _localProvider?.Resume();
            _player?.Play();
        }
    }

    public void Stop()
    {
        lock (_gate)
        {
            _playbackCts?.Cancel();
            _localProvider?.Stop();
            _player?.Stop();
            _player?.Close();
            _player = null;
            _localProvider = null;
            _audioDuckingService.EndDucking();
            CleanupTempFile();
            _logger.LogInformation("Playback stopped.");
        }
    }

    public void SetSpeed(double speed)
    {
        _localProvider?.SetSpeed(speed);
        if (_player is not null)
        {
            _player.SpeedRatio = Math.Clamp(speed, 0.5, 2.0);
        }
    }

    private void CleanupTempFile()
    {
        if (_tempFile is null)
        {
            return;
        }

        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // Best effort cleanup for files that may still be held by MediaPlayer.
        }
        finally
        {
            _tempFile = null;
        }
    }

    private async Task BeginAudioDuckingIfEnabledAsync(CancellationToken ct)
    {
        var settings = await _settingsService.LoadAsync(ct);
        if (!settings.DuckOtherAudio)
        {
            _logger.LogDebug("Audio ducking disabled.");
            return;
        }

        await _audioDuckingService.BeginDuckingAsync(settings.DuckOtherAudioVolumePercent, ct);
    }
}
