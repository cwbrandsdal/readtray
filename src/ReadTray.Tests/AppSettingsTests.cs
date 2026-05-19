using FluentAssertions;
using ReadTray.Core;

namespace ReadTray.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void Default_hotkeys_match_mvp_shortcuts()
    {
        var settings = new AppSettings();

        settings.ReadSelectedHotkey.ToString().Should().Be("Ctrl+Shift+F12");
        settings.ReadClipboardHotkey.ToString().Should().Be("Ctrl+Alt+Shift+Space");
    }
}
