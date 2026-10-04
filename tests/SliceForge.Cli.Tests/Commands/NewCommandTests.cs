using SliceForge.Cli.Commands;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Tests.Support;

namespace SliceForge.Cli.Tests.Commands;

public sealed class NewCommandTests
{
    [Theory]
    [InlineData("-n", "DispatchFlow")]
    [InlineData("--name", "DispatchFlow")]
    public async Task New_accepts_both_project_name_option_forms(string option, string name)
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(["new", option, name], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(["new", "list", "sliceforge-api"], processes.Calls[0]);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow"], processes.Calls[1]);
    }

    [Theory]
    [InlineData("-o")]
    [InlineData("--output")]
    public async Task New_forwards_output_directory_as_one_safe_argument(string outputOption)
    {
        const string outputPath = "./project output/final";
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "--name", "DispatchFlow", outputOption, outputPath],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow", "--output", outputPath], processes.Calls[1]);
    }

    [Fact]
    public async Task New_prompts_for_a_missing_name_in_an_interactive_terminal()
    {
        TestConsole console = new("PromptedFlow\n");
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(["new"], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("Project name:", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(["new", "sliceforge-api", "-n", "PromptedFlow"], processes.Calls[1]);
    }

    [Fact]
    public async Task New_requires_a_name_when_input_is_redirected()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["new"], console, processes);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: sliceforge new --name <project-name>", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Empty(processes.Calls);
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("bad-name")]
    [InlineData(" ")]
    public async Task New_rejects_names_that_cannot_be_safe_application_identifiers(string name)
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "--name", name], console, processes);

        Assert.Equal(2, exitCode);
        Assert.Contains("project name", console.ErrorWriter.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public async Task New_generates_directly_when_template_is_installed_and_forwards_child_output_and_exit_code()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "Template list\nSliceForge Minimal API sliceforge-api [C#]\n", string.Empty));
        processes.Enqueue(new ProcessResult(19, "generated output\n", "sdk error\n"));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(19, exitCode);
        Assert.Equal("generated output\n", console.OutputWriter.ToString());
        Assert.Equal("sdk error\n", console.ErrorWriter.ToString());
        Assert.Equal(2, processes.Calls.Count);
        Assert.DoesNotContain(processes.Calls, call => call.Contains("install", StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("y\n")]
    [InlineData("\n")]
    public async Task New_requests_explicit_confirmation_before_installing_a_missing_template(string confirmation)
    {
        TestConsole console = new(confirmation);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Installed template.\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Generated app.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("SliceForge.Templates 0.1.0-preview.2 is not installed.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("Install it now? [Y/n]", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(["new", "install", "SliceForge.Templates@0.1.0-preview.2"], processes.Calls[1]);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow"], processes.Calls[2]);
    }

    [Fact]
    public async Task New_declined_template_install_does_not_install_or_generate()
    {
        TestConsole console = new("DispatchFlow\nn\n");
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Single(processes.Calls);
        Assert.Contains("Install it now? [Y/n]", console.ErrorWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_fails_noninteractively_with_install_instructions_for_a_missing_template()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("SliceForge.Templates is required.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("dotnet new install SliceForge.Templates@0.1.0-preview.2", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Single(processes.Calls);
    }

    [Fact]
    public async Task New_passes_the_invocation_cancellation_token_to_the_sdk_process()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = new();

        CancellationToken observedToken = default;
        processes.Enqueue(new ProcessResult(0, "Template Name Short Name\nSliceForge Minimal API sliceforge-api\n", string.Empty));
        processes.Enqueue((_, token) =>
        {
            observedToken = token;
            cancellationTokenSource.Cancel();
            return Task.FromCanceled<ProcessResult>(token);
        });

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes, cancellationTokenSource.Token);

        Assert.Equal(130, exitCode);
        Assert.True(observedToken.CanBeCanceled);
        Assert.True(observedToken.IsCancellationRequested);
    }

    private static FakeProcessRunner TemplatePresentThenCreate()
    {
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "Template Name Short Name Language\nSliceForge Minimal API sliceforge-api [C#]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Created.\n", string.Empty));
        return processes;
    }
}
