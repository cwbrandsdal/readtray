using System.Windows;
using System.IO;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ReadTray.App.Services;
using ReadTray.Core;
using ReadTray.Infrastructure;
using ReadTray.Tts.ElevenLabs;
using ReadTray.Tts.Windows;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Text.Json;

namespace ReadTray.App;

public partial class App : System.Windows.Application
{
    private ServiceProvider? _services;
    private TrayService? _trayService;
    private GlobalHotkeyService? _hotkeyService;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadTray", "logs", "readtray-.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        var debugLoggingEnabled = ReadDebugLoggingEnabled();
        var logLevelSwitch = new LoggingLevelSwitch(debugLoggingEnabled ? LogEventLevel.Debug : LogEventLevel.Information);
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.ControlledBy(logLevelSwitch)
            .Enrich.WithProperty("ProcessId", Environment.ProcessId)
            .WriteTo.File(
                logPath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [pid:{ProcessId}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("ReadTray starting. Version={Version} BaseDirectory={BaseDirectory} LogPath={LogPath} DebugLogging={DebugLogging}",
            typeof(App).Assembly.GetName().Version,
            AppContext.BaseDirectory,
            Path.GetDirectoryName(logPath),
            debugLoggingEnabled);

        _services = ConfigureServices();
        _trayService = _services.GetRequiredService<TrayService>();
        _hotkeyService = _services.GetRequiredService<GlobalHotkeyService>();
        _trayService.Start();
        await _hotkeyService.StartAsync(CancellationToken.None);
        Log.Information("ReadTray startup complete.");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddSerilog(dispose: false));
        services.AddSingleton<ITextCleaningService, TextCleaningService>();
        services.AddSingleton<ISecretStore, DpapiSecretStore>();
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<ITextCaptureService, ClipboardTextCaptureService>();
        services.AddSingleton<IAudioDuckingService, AudioDuckingService>();
        services.AddSingleton<IUpdateService, GitHubReleaseUpdateService>();
        services.AddSingleton<ISpeechPlaybackService, SpeechPlaybackService>();
        services.AddSingleton<WindowsTtsProvider>();
        services.AddSingleton<ElevenLabsTtsProvider>();
        services.AddSingleton<ITtsProvider>(sp => sp.GetRequiredService<WindowsTtsProvider>());
        services.AddSingleton<ITtsProvider>(sp => sp.GetRequiredService<ElevenLabsTtsProvider>());
        services.AddSingleton<ReadCoordinator>();
        services.AddSingleton<TrayService>();
        services.AddSingleton<GlobalHotkeyService>();
        services.AddTransient<FloatingPlayerWindow>();
        services.AddTransient<SettingsWindow>();
        return services.BuildServiceProvider();
    }

    private static bool ReadDebugLoggingEnabled()
    {
        var settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReadTray", "settings.json");
        if (!File.Exists(settingsPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
            return document.RootElement.TryGetProperty("DebugLoggingEnabled", out var value) && value.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return false;
        }
    }
}
