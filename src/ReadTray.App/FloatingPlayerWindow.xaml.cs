using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using ReadTray.App.Services;
using ReadTray.Core;
using Forms = System.Windows.Forms;

namespace ReadTray.App;

public partial class FloatingPlayerWindow : Window
{
    private readonly ReadCoordinator _coordinator;
    private readonly ISettingsService _settingsService;
    private readonly IServiceProvider _services;
    private readonly System.Windows.Threading.DispatcherTimer _hideTimer = new();
    private string _text = string.Empty;
    private bool _manual;

    public FloatingPlayerWindow(ReadCoordinator coordinator, ISettingsService settingsService, IServiceProvider services)
    {
        InitializeComponent();
        _coordinator = coordinator;
        _settingsService = settingsService;
        _services = services;
        Loaded += async (_, _) => SpeedSlider.Value = (await _settingsService.LoadAsync(CancellationToken.None)).Speed;
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer.Stop();
            Hide();
        };
    }

    public void SetContent(string text, string? status, bool manual)
    {
        _text = text;
        _manual = manual;
        StatusText.Text = status ?? string.Empty;
        ManualInput.Visibility = manual ? Visibility.Visible : Visibility.Collapsed;
        PreviewHost.Visibility = manual ? Visibility.Collapsed : Visibility.Visible;
        PreviewText.Text = text.Length > 450 ? text[..450] + "..." : text;
        if (manual) ManualInput.Text = text;
        _hideTimer.Stop();
    }

    public void SetStatus(string status) => StatusText.Text = status;
    public void HideAfter(TimeSpan delay)
    {
        _hideTimer.Stop();
        _hideTimer.Interval = delay;
        _hideTimer.Start();
    }

    public void ShowNearCursor()
    {
        var screen = Forms.Screen.FromPoint(Forms.Cursor.Position);
        Left = Math.Min(Forms.Cursor.Position.X + 16, screen.WorkingArea.Right - Width - 12);
        Top = Math.Min(Forms.Cursor.Position.Y + 16, screen.WorkingArea.Bottom - Height - 12);
        Show();
        Activate();
    }

    private async void Play_Click(object sender, RoutedEventArgs e)
    {
        var text = _manual ? ManualInput.Text : _text;
        if (!string.IsNullOrWhiteSpace(text))
        {
            await _coordinator.ReadManualTextAsync(text);
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e) => _coordinator.Pause();
    private async void Restart_Click(object sender, RoutedEventArgs e) => await _coordinator.RestartAsync();
    private void Stop_Click(object sender, RoutedEventArgs e) => _coordinator.Stop();
    private void Copy_Click(object sender, RoutedEventArgs e) { if (!string.IsNullOrWhiteSpace(_text)) System.Windows.Clipboard.SetText(_text); }
    private void Settings_Click(object sender, RoutedEventArgs e) => _services.GetRequiredService<SettingsWindow>().Show();
    private void Hide_Click(object sender, RoutedEventArgs e) => Hide();
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
        }
    }

    private async void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!IsLoaded) return;
        var settings = await _settingsService.LoadAsync(CancellationToken.None);
        settings.Speed = Math.Round(e.NewValue, 2);
        SpeedText.Text = $"{settings.Speed:0.00}x";
        _coordinator.SetSpeed(settings.Speed);
        await _settingsService.SaveAsync(settings, CancellationToken.None);
    }

    private void Window_Deactivated(object sender, EventArgs e) { }
    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { if (e.Key == Key.Escape) Hide(); }
}
