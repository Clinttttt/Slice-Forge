using SliceForge.Cli.Commands;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Tests.Support;

namespace SliceForge.Cli.Tests.Commands;

public sealed class CliCommandTests
{
    [Fact]
    public async Task Root_command_shows_the_brand_screen_when_no_arguments_are_supplied()
    {
        TestConsole console = new();
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync([], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("SliceForge", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Empty(console.ErrorWriter.ToString());
    }

    [Fact]
    public async Task Help_uses_normal_command_line_help_instead_of_the_brand_screen()
    {
        TestConsole console = new();
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["--help"], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("Usage:", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("new", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("╭", console.OutputWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Version_command_writes_only_the_package_version_and_newline()
    {
        TestConsole console = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["version"], console, new FakeProcessRunner());

        Assert.Equal(0, exitCode);
        Assert.Equal($"0.1.0-preview.3{Environment.NewLine}", console.OutputWriter.ToString());
        Assert.Empty(console.ErrorWriter.ToString());
    }

    [Fact]
    public async Task Root_version_option_writes_only_the_package_version_and_newline()
    {
        TestConsole console = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["--version"], console, new FakeProcessRunner());

        Assert.Equal(0, exitCode);
        Assert.Equal($"0.1.0-preview.3{Environment.NewLine}", console.OutputWriter.ToString());
        Assert.Empty(console.ErrorWriter.ToString());
    }
}
