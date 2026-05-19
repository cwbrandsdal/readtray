using System.Diagnostics;
using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using ReadTray.Core;

namespace ReadTray.Infrastructure;

public sealed class AudioDuckingService : IAudioDuckingService
{
    private readonly ILogger<AudioDuckingService> _logger;
    private readonly object _gate = new();
    private readonly Dictionary<string, SessionSnapshot> _snapshots = new();

    public AudioDuckingService(ILogger<AudioDuckingService> logger)
    {
        _logger = logger;
    }

    public Task BeginDuckingAsync(double volumePercent, CancellationToken ct)
    {
        lock (_gate)
        {
            EndDuckingCore();
            var target = (float)Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
            using var enumerator = new MMDeviceEnumerator();
            using var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            var sessions = device.AudioSessionManager.Sessions;
            var currentPid = Environment.ProcessId;
            var ducked = 0;

            for (var i = 0; i < sessions.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                var session = sessions[i];
                try
                {
                    if (session.GetProcessID == 0 || session.GetProcessID == currentPid)
                    {
                        continue;
                    }

                    var key = $"{session.GetProcessID}:{session.DisplayName}:{i}";
                    var volume = session.SimpleAudioVolume;
                    _snapshots[key] = new SessionSnapshot(volume, volume.Volume, volume.Mute);
                    volume.Volume = target;
                    if (target > 0)
                    {
                        volume.Mute = false;
                    }

                    ducked++;
                    _logger.LogDebug("Ducked audio session. ProcessId={ProcessId} ProcessName={ProcessName} OldVolume={OldVolume:0.00} TargetVolume={TargetVolume:0.00}",
                        session.GetProcessID,
                        TryGetProcessName((int)session.GetProcessID),
                        _snapshots[key].Volume,
                        target);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping audio session during ducking.");
                }
            }

            _logger.LogInformation("Audio ducking applied. Sessions={SessionCount} TargetVolumePercent={VolumePercent}", ducked, volumePercent);
        }

        return Task.CompletedTask;
    }

    public void EndDucking()
    {
        lock (_gate)
        {
            EndDuckingCore();
        }
    }

    private void EndDuckingCore()
    {
        var restored = 0;
        foreach (var snapshot in _snapshots.Values)
        {
            try
            {
                snapshot.VolumeControl.Volume = snapshot.Volume;
                snapshot.VolumeControl.Mute = snapshot.Mute;
                restored++;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unable to restore audio session volume.");
            }
        }

        if (_snapshots.Count > 0)
        {
            _logger.LogInformation("Audio ducking restored. Sessions={SessionCount}", restored);
        }

        _snapshots.Clear();
    }

    private static string? TryGetProcessName(int processId)
    {
        try
        {
            return Process.GetProcessById(processId).ProcessName;
        }
        catch
        {
            return null;
        }
    }

    private sealed record SessionSnapshot(SimpleAudioVolume VolumeControl, float Volume, bool Mute);
}
