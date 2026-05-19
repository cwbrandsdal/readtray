using System.Windows;
using ReadTray.Core;

namespace ReadTray.App;

public partial class SettingsWindow : Window
{
    private readonly ISettingsService _settingsService;
    private readonly IEnumerable<ITtsProvider> _providers;
    private AppSettings _settings = new();

    public SettingsWindow(ISettingsService settingsService, IEnumerable<ITtsProvider> providers)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _providers = providers;
        Loaded += async (_, _) => await LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        _settings = await _settingsService.LoadAsync(CancellationToken.None);
        ProviderBox.ItemsSource = _providers.ToArray();
        ProviderBox.SelectedValue = _settings.SelectedProviderId;
        SpeedSlider.Value = _settings.Speed;
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
        ElevenLabsModelBox.Text = _settings.ElevenLabsModelId;
        ElevenLabsCustomVoiceBox.Text = _settings.ElevenLabsCustomVoiceId ?? string.Empty;
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
    private async void RefreshVoices_Click(object sender, RoutedEventArgs e) => await LoadVoicesAsync();

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
        _settings.ElevenLabsModelId = string.IsNullOrWhiteSpace(ElevenLabsModelBox.Text) ? "eleven_turbo_v2_5" : ElevenLabsModelBox.Text.Trim();
        _settings.ElevenLabsCustomVoiceId = string.IsNullOrWhiteSpace(ElevenLabsCustomVoiceBox.Text) ? null : ElevenLabsCustomVoiceBox.Text.Trim();
        await _settingsService.SaveAsync(_settings, CancellationToken.None);
        Hide();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
}
