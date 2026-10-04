using SliceForge.Cli.Console;

namespace SliceForge.Cli.Tests.Console;

public sealed class RootScreenTests
{
    [Fact]
    public void Root_screen_shows_compact_brand_commands_and_version_without_ansi()
    {
        using StringWriter output = new();

        RootScreen.Write(output, "0.1.0-preview.1", supportsUnicode: true);

        string text = output.ToString();
        Assert.Contains("SliceForge", text, StringComparison.Ordinal);
        Assert.Contains("Composable Vertical Slice Toolkit for .NET 10", text, StringComparison.Ordinal);
        Assert.Contains("v0.1.0-preview.1", text, StringComparison.Ordinal);
        Assert.Contains("Author: Clint Villanueva", text, StringComparison.Ordinal);
        Assert.Contains("new       Create a SliceForge application", text, StringComparison.Ordinal);
        Assert.Contains("doctor    Check the environment", text, StringComparison.Ordinal);
        Assert.Contains("version   Show version", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Root_screen_uses_ascii_when_unicode_is_not_supported()
    {
        using StringWriter output = new();

        RootScreen.Write(output, "0.1.0-preview.1", supportsUnicode: false);

        string text = output.ToString();
        Assert.Contains("+", text, StringComparison.Ordinal);
        Assert.Contains("| SliceForge", text, StringComparison.Ordinal);
        Assert.DoesNotContain("╭", text, StringComparison.Ordinal);
        Assert.DoesNotContain("✓", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", text, StringComparison.Ordinal);
    }
}
