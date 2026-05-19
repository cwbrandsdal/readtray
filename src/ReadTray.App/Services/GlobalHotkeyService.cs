using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Extensions.Logging;
using ReadTray.Core;

namespace ReadTray.App.Services;

public sealed class GlobalHotkeyService : IDisposable
{
    private const int WmHotkey = 0x0312;
    private static readonly IntPtr HwndMessage = new(-3);
    private const int ReadSelectedId = 100;
    private const int ReadClipboardId = 101;
    private readonly ISettingsService _settingsService;
    private readonly ReadCoordinator _coordinator;
    private readonly ILogger<GlobalHotkeyService> _logger;
    private HwndSource? _source;

    public GlobalHotkeyService(ISettingsService settingsService, ReadCoordinator coordinator, ILogger<GlobalHotkeyService> logger)
    {
        _settingsService = settingsService;
        _coordinator = coordinator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var parameters = new HwndSourceParameters("ReadTrayHotkeySink")
        {
            ParentWindow = HwndMessage,
            Width = 0,
            Height = 0,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x00000080
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        var settings = await _settingsService.LoadAsync(ct);
        Register(ReadSelectedId, settings.ReadSelectedHotkey);
        Register(ReadClipboardId, settings.ReadClipboardHotkey);
        _logger.LogInformation("Global hotkey service started. ReadSelected={ReadSelectedHotkey} ReadClipboard={ReadClipboardHotkey}", settings.ReadSelectedHotkey, settings.ReadClipboardHotkey);
    }

    private void Register(int id, HotkeyGesture gesture)
    {
        var modifiers = 0u;
        if (gesture.Alt) modifiers |= 0x0001;
        if (gesture.Control) modifiers |= 0x0002;
        if (gesture.Shift) modifiers |= 0x0004;
        if (gesture.Win) modifiers |= 0x0008;
        var key = KeyInterop.VirtualKeyFromKey((Key)Enum.Parse(typeof(Key), gesture.Key, true));
        if (!RegisterHotKey(_source!.Handle, id, modifiers, (uint)key))
        {
            _logger.LogWarning("Unable to register hotkey {Hotkey}.", gesture);
            System.Windows.MessageBox.Show($"ReadTray could not register {gesture}. Choose another shortcut in Settings.", "ReadTray hotkey conflict", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        else
        {
            _logger.LogInformation("Registered global hotkey {Hotkey} with id {HotkeyId}.", gesture, id);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey)
        {
            handled = true;
            _logger.LogInformation("Global hotkey received. Id={HotkeyId}", wParam.ToInt32());
            _ = wParam.ToInt32() == ReadClipboardId ? _coordinator.ReadClipboardAsync() : _coordinator.ReadSelectedAsync();
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_source is null) return;
        UnregisterHotKey(_source.Handle, ReadSelectedId);
        UnregisterHotKey(_source.Handle, ReadClipboardId);
        _source.Dispose();
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
