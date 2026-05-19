using System.Runtime.CompilerServices;
using System.Speech.Synthesis;
using ReadTray.Core;

namespace ReadTray.Tts.Windows;

public sealed class WindowsTtsProvider : ILocalSpeechProvider, IDisposable
{
    private readonly SpeechSynthesizer _synthesizer = new();
    private TaskCompletionSource? _currentCompletion;

    public string Id => "windows";
    public string DisplayName => "Windows";

    public Task<IReadOnlyList<TtsVoice>> GetVoicesAsync(CancellationToken ct)
    {
        IReadOnlyList<TtsVoice> voices = _synthesizer.GetInstalledVoices()
            .Where(v => v.Enabled)
            .Select(v => new TtsVoice(v.VoiceInfo.Name, v.VoiceInfo.Name, v.VoiceInfo.Culture.Name))
            .ToArray();
        return Task.FromResult(voices);
    }

    public async IAsyncEnumerable<AudioChunk> StreamSpeechAsync(TtsRequest request, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task SpeakAsync(TtsRequest request, CancellationToken ct)
    {
        Stop();
        if (!string.IsNullOrWhiteSpace(request.VoiceId))
        {
            _synthesizer.SelectVoice(request.VoiceId);
        }

        _synthesizer.Rate = SpeedToRate(request.Speed);
        _currentCompletion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Completed(object? sender, SpeakCompletedEventArgs args)
        {
            _synthesizer.SpeakCompleted -= Completed;
            if (args.Error is not null)
            {
                _currentCompletion.TrySetException(args.Error);
            }
            else
            {
                _currentCompletion.TrySetResult();
            }
        }

        _synthesizer.SpeakCompleted += Completed;
        using var registration = ct.Register(Stop);
        _synthesizer.SpeakAsync(request.Text);
        return _currentCompletion.Task;
    }

    public void Pause() => _synthesizer.Pause();
    public void Resume() => _synthesizer.Resume();
    public void Stop() => _synthesizer.SpeakAsyncCancelAll();
    public void SetSpeed(double speed) => _synthesizer.Rate = SpeedToRate(speed);
    public void Dispose() => _synthesizer.Dispose();

    private static int SpeedToRate(double speed)
    {
        return (int)Math.Clamp(Math.Round((speed - 1.0) * 8), -10, 10);
    }
}
