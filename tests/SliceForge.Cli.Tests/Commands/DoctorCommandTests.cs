using SliceForge.Cli.Commands;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Tests.Support;

namespace SliceForge.Cli.Tests.Commands;

public sealed class DoctorCommandTests
{
    [Fact]
    public async Task Doctor_reports_a_missing_dotnet_sdk_without_installing_or_restoring()
    {
        TestConsole console = new(supportsUnicode: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(127, string.Empty, "dotnet missing"));

        int exitCode = await CliCommandFactory.InvokeAsync(["doctor"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("✗ .NET SDK missing", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("✗ .NET 10 SDK missing", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Single(processes.Calls);
        Assert.DoesNotContain(processes.Calls.SelectMany(call => call), argument => argument is "install" or "restore");
    }

    [Fact]
    public async Task Doctor_reports_when_the_dotnet_10_sdk_is_missing()
    {
        TestConsole console = new(supportsUnicode: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "8.0.404 [sdk]\n9.0.203 [sdk]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "SliceForge Minimal API sliceforge-api [C#]\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["doctor"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("✓ .NET SDK detected", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("✗ .NET 10 SDK missing", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("✓ SliceForge template installed", console.OutputWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Doctor_reports_a_missing_template_and_prints_the_install_command()
    {
        TestConsole console = new(supportsUnicode: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "10.0.401 [sdk]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["doctor"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("✗ SliceForge template not installed", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("dotnet new install SliceForge.Templates@0.1.0-preview.2", console.OutputWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Doctor_reports_ready_when_sdk_net10_and_template_are_available()
    {
        TestConsole console = new(supportsUnicode: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "8.0.404 [sdk]\n10.0.401 [sdk]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "SliceForge Minimal API sliceforge-api [C#]\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["doctor"], console, processes);

        Assert.True(exitCode == 0, $"Exit code {exitCode}. Output: {console.OutputWriter}");
        Assert.Contains("✓ .NET SDK detected", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("✓ .NET 10 SDK detected", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("✓ SliceForge template installed", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("Environment ready.", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(2, processes.Calls.Count);
        Assert.DoesNotContain(processes.Calls.SelectMany(call => call), argument => argument is "install" or "restore");
    }

    [Fact]
    public async Task Doctor_uses_plain_ascii_indicators_when_unicode_is_unavailable()
    {
        TestConsole console = new(supportsUnicode: false);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "10.0.401 [sdk]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "sliceforge-api\n", string.Empty));

        await CliCommandFactory.InvokeAsync(["doctor"], console, processes);

        Assert.Contains("+ .NET SDK detected", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("\u001b", console.OutputWriter.ToString(), StringComparison.Ordinal);
    }
}
