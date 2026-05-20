using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using ReadTray.Core;
using Forms = System.Windows.Forms;

namespace ReadTray.App.Services;

public sealed class TrayService : IDisposable
{
    private readonly ReadCoordinator _coordinator;
    private readonly IServiceProvider _services;
    private readonly ISettingsService _settingsService;
    private readonly IUpdateService _updateService;
    private readonly ILogger<TrayService> _logger;
    private readonly string _iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "ReadTray.ico");
    private Forms.NotifyIcon? _notifyIcon;
    private Icon? _trayIcon;

    public TrayService(ReadCoordinator coordinator, IServiceProvider services, ISettingsService settingsService, IUpdateService updateService, ILogger<TrayService> logger)
    {
        _coordinator = coordinator;
        _services = services;
        _settingsService = settingsService;
        _updateService = updateService;
        _logger = logger;
    }

    public void Start()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Read selected text", null, (_, _) => _ = _coordinator.ReadSelectedAsync());
        menu.Items.Add("Read clipboard", null, (_, _) => _ = _coordinator.ReadClipboardAsync());
        menu.Items.Add("Stop reading", null, (_, _) => _coordinator.Stop());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Check for updates", null, async (_, _) => await CheckForUpdatesAsync(showUpToDate: true));
        menu.Items.Add("Settings", null, (_, _) => ShowSettings());
        menu.Items.Add("Open logs", null, (_, _) => OpenLogs());
        menu.Items.Add("About", null, (_, _) => System.Windows.MessageBox.Show("ReadTray\nFast selected-text-to-speech for Windows.", "About ReadTray"));
        menu.Items.Add("Quit", null, (_, _) => System.Windows.Application.Current.Shutdown());

        _trayIcon = File.Exists(_iconPath) ? new Icon(_iconPath) : SystemIcons.Application;
        _notifyIcon = new Forms.NotifyIcon { Icon = _trayIcon, Text = "ReadTray", Visible = true, ContextMenuStrip = menu };
        _notifyIcon.DoubleClick += (_, _) => ShowSettings();
        _ = CheckForUpdatesOnStartupAsync();
    }

    private void ShowSettings()
    {
        var window = (SettingsWindow)_services.GetService(typeof(SettingsWindow))!;
        window.Show();
        window.Activate();
    }

    private static void OpenLogs()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReadTray", "logs");
        Directory.CreateDirectory(dir);
        Process.Start(new ProcessStartInfo { FileName = dir, UseShellExecute = true });
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        try
        {
            var settings = await _settingsService.LoadAsync(CancellationToken.None);
            if (settings.CheckForUpdatesOnStartup)
            {
                await CheckForUpdatesAsync(showUpToDate: false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Startup update check failed.");
        }
    }

    private async Task CheckForUpdatesAsync(bool showUpToDate)
    {
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(CancellationToken.None);
            if (!result.IsUpdateAvailable)
            {
                if (showUpToDate)
                {
                    System.Windows.MessageBox.Show(result.Message ?? "ReadTray is up to date.", "ReadTray updates", MessageBoxButton.OK, MessageBoxImage.Information);
                }

                return;
            }

            var message = $"{result.Message}\n\nCurrent: {result.CurrentVersion}\nLatest: {result.LatestVersion}\nPackage: {result.AssetName}\n\nDownload and install this update now?";
            var open = System.Windows.MessageBox.Show(message, "ReadTray update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (open == MessageBoxResult.Yes)
            {
                await DownloadAndApplyUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Update check failed.");
            if (showUpToDate)
            {
                System.Windows.MessageBox.Show($"Update check failed: {ex.Message}", "ReadTray updates", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private async Task DownloadAndApplyUpdateAsync()
    {
        var progress = new Progress<int>(value =>
        {
            if (_notifyIcon is not null)
            {
                _notifyIcon.Text = $"ReadTray updating {value}%";
            }
        });

        var result = await _updateService.DownloadAndApplyLatestUpdateAsync(progress, CancellationToken.None);
        if (!result.IsUpdateAvailable)
        {
            System.Windows.MessageBox.Show(result.Message ?? "ReadTray is up to date.", "ReadTray updates", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    public void Dispose()
    {
        if (_notifyIcon is null) return;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayIcon?.Dispose();
    }
}
