using System.Windows;
using ReadTray.Core;
using ReadTray.Tts.ElevenLabs;

namespace ReadTray.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IEnumerable<ITtsProvider> _providers;
    private readonly IUpdateService _updateService;
    private AppSettings _settings = new();

    public SettingsWindow(ISettingsService settingsService, IEnumerable<ITtsProvider> providers, IUpdateService updateService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _providers = providers;
        _updateService = updateService;
        VersionText.Text = $"v{AppVersionInfo.Current}";
        Loaded += async (_, _) => await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadAsync(CancellationToken.None);
        ProviderBox.ItemsSource = _providers.ToArray();
        ProviderBox.SelectedValue = _settings.SelectedProviderId;
        SpeedSlider.Value = _settings.Speed;
        UpdateSpeedValueText(_settings.Speed);
        PrivacyModeBox.IsChecked = _settings.PrivacyMode;
        DebugLoggingBox.IsChecked = _settings.DebugLoggingEnabled;
        DebugTextPreviewBox.IsChecked = _settings.DebugLogTextPreview;
        RestoreClipboardBox.IsChecked = _settings.RestoreClipboardText;
        AutoHideBox.IsChecked = _settings.AutoHidePlayer;
        DuckAudioBox.IsChecked = _settings.DuckOtherAudio;
        DuckAudioVolumeSlider.Value = _settings.DuckOtherAudioVolumePercent;
        DuckAudioVolumeText.Text = $"{_settings.DuckOtherAudioVolumePercent:0}%";
        CheckForUpdatesBox.IsChecked = _settings.CheckForUpdatesOnStartup;
        DuckAudioVolumeSlider.ValueChanged += (_, args) => DuckAudioVolumeText.Text = $"{args.NewValue:0}%";
        ElevenLabsKeyBox.Password = _settings.ElevenLabsApiKey ?? string.Empty;
        ElevenLabsCustomVoiceBox.Text = _settings.ElevenLabsCustomVoiceId ?? string.Empty;
        await LoadElevenLabsModelsAsync();
        await LoadVoicesAsync();
    }

    private async Task LoadVoicesAsync()
    {
        if (ProviderBox.SelectedValue is not string providerId) return;
        var provider = _providers.First(p => p.Id == providerId);
        VoiceBox.ItemsSource = await provider.GetVoicesAsync(CancellationToken.None);
        VoiceBox.SelectedValue = _settings.SelectedVoiceByProvider.GetValueOrDefault(providerId);
    }

    private async void ProviderBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => await LoadVoicesAsync();
    private async void RefreshVoices_Click(object sender, RoutedEventArgs e)
    {
        await LoadElevenLabsModelsAsync();
        await LoadVoicesAsync();
    }

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateSpeedValueText(e.NewValue);
    }

    private void UpdateSpeedValueText(double speed)
    {
        if (SpeedValueText is not null)
        {
            SpeedValueText.Text = $"{speed * 100:0}%";
        }
    }

    private async Task LoadElevenLabsModelsAsync()
    {
        var selectedModel = string.IsNullOrWhiteSpace(_settings.ElevenLabsModelId) ? "eleven_turbo_v2_5" : _settings.ElevenLabsModelId;
        var models = ElevenLabsTtsProvider.GetDefaultModels();
        ElevenLabsModelStatusText.Text = "Refresh voices also refreshes available ElevenLabs models.";

        if (_providers.FirstOrDefault(provider => provider.Id == "elevenlabs") is ElevenLabsTtsProvider elevenLabs)
        {
            try
            {
                models = await elevenLabs.GetModelsAsync(CancellationToken.None);
                ElevenLabsModelStatusText.Text = "Loaded available ElevenLabs models from the API.";
            }
            catch (Exception ex)
            {
                ElevenLabsModelStatusText.Text = $"Could not load ElevenLabs models from the API. Using defaults. {ex.Message}";
            }
        }

        if (!models.Any(model => string.Equals(model.Id, selectedModel, StringComparison.OrdinalIgnoreCase)))
        {
            models = models.Concat(new[] { new TtsModel(selectedModel, $"{selectedModel} (custom)") }).ToArray();
        }

        ElevenLabsModelBox.ItemsSource = models;
        ElevenLabsModelBox.SelectedValue = selectedModel;
        ElevenLabsModelBox.Text = selectedModel;
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        await CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        UpdateStatusText.Text = "Checking for updates...";
        try
        {
            var result = await _updateService.CheckForUpdatesAsync(CancellationToken.None);
            if (!result.IsUpdateAvailable)
            {
                UpdateStatusText.Text = result.Message ?? "ReadTray is up to date.";
                return;
            }

            UpdateStatusText.Text = $"{result.Message} Current: {result.CurrentVersion}. Latest: {result.LatestVersion}.";
            var message = $"{result.Message}\n\nCurrent: {result.CurrentVersion}\nLatest: {result.LatestVersion}\nPackage: {result.AssetName}\n\nDownload and install this update now?";
            var install = System.Windows.MessageBox.Show(message, "ReadTray update available", MessageBoxButton.YesNo, MessageBoxImage.Information);
            if (install == MessageBoxResult.Yes)
            {
                await DownloadAndApplyUpdateAsync();
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"Update check failed: {ex.Message}";
            System.Windows.MessageBox.Show($"Update check failed: {ex.Message}", "ReadTray updates", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async Task DownloadAndApplyUpdateAsync()
    {
        var progress = new Progress<int>(value => UpdateStatusText.Text = $"Downloading update... {value}%");
        var result = await _updateService.DownloadAndApplyLatestUpdateAsync(progress, CancellationToken.None);
        if (!result.IsUpdateAvailable)
        {
            UpdateStatusText.Text = result.Message ?? "ReadTray is up to date.";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        _settings.SelectedProviderId = ProviderBox.SelectedValue as string ?? "windows";
        if (VoiceBox.SelectedValue is string voice)
        {
            _settings.SelectedVoiceByProvider[_settings.SelectedProviderId] = voice;
        }

        _settings.Speed = Math.Round(SpeedSlider.Value, 2);
        _settings.PrivacyMode = PrivacyModeBox.IsChecked == true;
        _settings.DebugLoggingEnabled = DebugLoggingBox.IsChecked == true;
        _settings.DebugLogTextPreview = DebugTextPreviewBox.IsChecked == true;
        _settings.RestoreClipboardText = RestoreClipboardBox.IsChecked == true;
        _settings.AutoHidePlayer = AutoHideBox.IsChecked == true;
        _settings.DuckOtherAudio = DuckAudioBox.IsChecked == true;
        _settings.DuckOtherAudioVolumePercent = Math.Round(DuckAudioVolumeSlider.Value, 0);
        _settings.CheckForUpdatesOnStartup = CheckForUpdatesBox.IsChecked == true;
        _settings.ElevenLabsApiKey = ElevenLabsKeyBox.Password;
        var selectedModel = ElevenLabsModelBox.SelectedValue as string ?? ElevenLabsModelBox.Text;
        _settings.ElevenLabsModelId = string.IsNullOrWhiteSpace(selectedModel) ? "eleven_turbo_v2_5" : selectedModel.Trim();
        _settings.ElevenLabsCustomVoiceId = string.IsNullOrWhiteSpace(ElevenLabsCustomVoiceBox.Text) ? null : ElevenLabsCustomVoiceBox.Text.Trim();
        await _settingsService.SaveAsync(_settings, CancellationToken.None);
        Hide();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
