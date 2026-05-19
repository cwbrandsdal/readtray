using FluentAssertions;
using ReadTray.Core;

namespace ReadTray.Tests;

public sealed class TextCleaningServiceTests
{
    [Fact]
    public void Clean_normalizes_spacing_without_losing_paragraphs()
    {
        var service = new TextCleaningService();

        var result = service.Clean("  Hello\t\tworld\r\n\r\n\r\nNext   line  ");

        result.Should().Be("Hello world\n\nNext line");
    }
}
