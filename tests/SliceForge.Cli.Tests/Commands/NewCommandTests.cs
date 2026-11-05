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
        Assert.Equal(0, console.InputReadCount);
    }

    [Theory]
    [InlineData("--validation", "--UseValidation", "true", "--UseObservability", "true")]
    [InlineData("--no-validation", "--UseValidation", "false", "--UseObservability", "true")]
    [InlineData("--observability", "--UseValidation", "true", "--UseObservability", "true")]
    [InlineData("--no-observability", "--UseValidation", "true", "--UseObservability", "false")]
    public async Task Explicit_capability_flags_forward_both_resolved_template_parameters(
        string flag,
        string validationParameter,
        string validationValue,
        string observabilityParameter,
        string observabilityValue)
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", flag],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["new", "sliceforge-api", "-n", "DispatchFlow", validationParameter, validationValue, observabilityParameter, observabilityValue],
            processes.Calls[1]);
        Assert.Equal(0, console.InputReadCount);
    }

    [Fact]
    public async Task Both_positive_capability_flags_are_forwarded_in_stable_order()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", "--validation", "--observability"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["new", "sliceforge-api", "-n", "DispatchFlow", "--UseValidation", "true", "--UseObservability", "true"],
            processes.Calls[1]);
    }

    [Fact]
    public async Task Both_negative_capability_flags_disable_both_optional_capabilities()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", "--no-validation", "--no-observability"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(
            ["new", "sliceforge-api", "-n", "DispatchFlow", "--UseValidation", "false", "--UseObservability", "false"],
            processes.Calls[1]);
    }

    [Fact]
    public async Task Capability_flag_skips_interactive_wizard_even_when_input_is_available()
    {
        TestConsole console = new("MustNotBeRead\n");
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", "--no-validation"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.DoesNotContain("Project capabilities", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
        Assert.Equal(
            ["new", "sliceforge-api", "-n", "DispatchFlow", "--UseValidation", "false", "--UseObservability", "true"],
            processes.Calls[1]);
    }

    [Theory]
    [InlineData("--validation", "--no-validation")]
    [InlineData("--observability", "--no-observability")]
    public async Task Contradictory_flags_fail_before_prompts_or_sdk_calls(string enabledFlag, string disabledFlag)
    {
        TestConsole console = new("MustNotBeRead\n");
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", enabledFlag, disabledFlag],
            console,
            processes);

        Assert.Equal(2, exitCode);
        Assert.Contains(enabledFlag, console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains(disabledFlag, console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Project capabilities", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public async Task Redirected_default_generation_uses_template_defaults_without_prompts_or_parameters()
    {
        TestConsole console = new(isInputRedirected: true);
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow"], processes.Calls[1]);
        Assert.DoesNotContain("Project capabilities", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
    }

    [Fact]
    public async Task Non_interactive_default_generation_uses_template_defaults_without_prompts()
    {
        TestConsole console = new();
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", "--non-interactive"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow"], processes.Calls[1]);
        Assert.DoesNotContain("Project capabilities", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
    }

    [Theory]
    [InlineData("--no-validation")]
    [InlineData("--non-interactive")]
    public async Task Flag_driven_generation_requires_a_name_without_prompting(string flag)
    {
        TestConsole console = new("MustNotBeRead\n");
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", flag],
            console,
            processes);

        Assert.Equal(2, exitCode);
        Assert.Contains("project name is required", console.ErrorWriter.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, console.InputReadCount);
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public async Task Interactive_wizard_prompts_in_order_summarizes_selection_and_forwards_output()
    {
        const string outputPath = "./project output/final";
        TestConsole console = new("  YeS  \n no \n yes \n");
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "--name", "DispatchFlow", "--output", outputPath],
            console,
            processes);

        string output = console.OutputWriter.ToString();
        Assert.Equal(0, exitCode);
        Assert.True(output.IndexOf("Use validation?", StringComparison.Ordinal) < output.IndexOf("Use observability?", StringComparison.Ordinal));
        Assert.Contains("Project: DispatchFlow", output, StringComparison.Ordinal);
        Assert.Contains("Framework: .NET 10", output, StringComparison.Ordinal);
        Assert.Contains("Output: ./project output/final", output, StringComparison.Ordinal);
        Assert.Contains("SliceForge.Validation", output, StringComparison.Ordinal);
        Assert.Contains("FluentValidation", output, StringComparison.Ordinal);
        Assert.DoesNotContain("SliceForge.Observability", output, StringComparison.Ordinal);
        Assert.Contains("Create project? [Y/n]", output, StringComparison.Ordinal);
        Assert.Equal(
            ["new", "sliceforge-api", "-n", "DispatchFlow", "--output", outputPath, "--UseValidation", "true", "--UseObservability", "false"],
            processes.Calls[1]);
    }

    [Fact]
    public async Task Interactive_name_prompt_remains_before_capability_prompts()
    {
        TestConsole console = new("PromptedFlow\n\n\n\n");
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(["new"], console, processes);

        string output = console.OutputWriter.ToString();
        Assert.Equal(0, exitCode);
        Assert.True(output.IndexOf("Project name:", StringComparison.Ordinal) < output.IndexOf("Use validation?", StringComparison.Ordinal));
        Assert.Equal(["new", "sliceforge-api", "-n", "PromptedFlow", "--UseValidation", "true", "--UseObservability", "true"], processes.Calls[1]);
        Assert.Equal(4, console.InputReadCount);
    }

    [Fact]
    public async Task Invalid_interactive_answers_are_rejected_and_prompted_again()
    {
        TestConsole console = new("perhaps\nyes\nyes\nyes\n");
        FakeProcessRunner processes = TemplatePresentThenCreate();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("Please answer yes or no.", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(4, console.InputReadCount);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow", "--UseValidation", "true", "--UseObservability", "true"], processes.Calls[1]);
    }

    [Theory]
    [InlineData("")]
    [InlineData("y\n")]
    [InlineData("y\ny\n")]
    public async Task End_of_input_never_confirms_or_generates(string input)
    {
        TestConsole console = new(input);
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(1, exitCode);
        Assert.Empty(processes.Calls);
        Assert.Contains("Project creation was not confirmed.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Declining_project_creation_stops_before_template_lookup()
    {
        TestConsole console = new("y\ny\nn\n");
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(1, exitCode);
        Assert.Empty(processes.Calls);
        Assert.Equal(3, console.InputReadCount);
    }

    [Fact]
    public async Task Interactive_wizard_does_not_depend_on_unicode_or_color_support()
    {
        TestConsole console = new("n\ny\nn\n", supportsUnicode: false);
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("SliceForge - New Application", console.OutputWriter.ToString(), StringComparison.Ordinal);
        Assert.Empty(processes.Calls);
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
    public async Task New_requires_a_name_when_input_is_redirected()
    {
        TestConsole console = new("MustNotBeRead\n", isInputRedirected: true);
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["new"], console, processes);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: sliceforge new --name <project-name>", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
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

    [Fact]
    public async Task New_requests_explicit_confirmation_before_installing_a_missing_template()
    {
        TestConsole console = new("yes\ny\ny\ny\n");
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Installed template.\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Generated app.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(0, exitCode);
        Assert.Contains("SliceForge.Templates 0.1.0-preview.4 is not installed.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("Install it now? [Y/n]", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(["new", "install", "SliceForge.Templates@0.1.0-preview.4"], processes.Calls[1]);
        Assert.Equal(["new", "sliceforge-api", "-n", "DispatchFlow", "--UseValidation", "true", "--UseObservability", "true"], processes.Calls[2]);
    }

    [Fact]
    public async Task New_declined_template_install_does_not_install_or_generate()
    {
        TestConsole console = new("y\ny\ny\nn\n");
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Single(processes.Calls);
        Assert.Contains("Install it now? [Y/n]", console.ErrorWriter.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task New_fails_without_prompting_for_a_missing_template_when_input_is_redirected()
    {
        TestConsole console = new("MustNotBeRead\n", isInputRedirected: true);
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "-n", "DispatchFlow"], console, processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("SliceForge.Templates is required.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Contains("dotnet new install SliceForge.Templates@0.1.0-preview.4", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Single(processes.Calls);
        Assert.Equal(0, console.InputReadCount);
    }

    [Fact]
    public async Task Non_interactive_missing_template_never_prompts_to_install()
    {
        TestConsole console = new("MustNotBeRead\n");
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow", "--non-interactive"],
            console,
            processes);

        Assert.Equal(1, exitCode);
        Assert.Contains("SliceForge.Templates is required.", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Install it now?", console.ErrorWriter.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, console.InputReadCount);
        Assert.Single(processes.Calls);
    }

    [Fact]
    public async Task End_of_input_during_template_install_confirmation_is_not_consent()
    {
        TestConsole console = new("y\ny\ny\n");
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "No templates found.\n", string.Empty));

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes);

        Assert.Equal(1, exitCode);
        Assert.Single(processes.Calls);
        Assert.Equal(4, console.InputReadCount);
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

    [Fact]
    public async Task New_cancellation_during_interactive_prompt_returns_130_without_sdk_calls()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        cancellationTokenSource.Cancel();
        TestConsole console = new();
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(
            ["new", "-n", "DispatchFlow"],
            console,
            processes,
            cancellationTokenSource.Token);

        Assert.Equal(130, exitCode);
        Assert.Empty(processes.Calls);
    }

    [Fact]
    public async Task New_help_lists_capability_and_non_interactive_options_without_running_sdk()
    {
        TestConsole console = new();
        FakeProcessRunner processes = new();

        int exitCode = await CliCommandFactory.InvokeAsync(["new", "--help"], console, processes);

        Assert.Equal(0, exitCode);
        string help = console.OutputWriter.ToString();
        Assert.Contains("--validation", help, StringComparison.Ordinal);
        Assert.Contains("--no-validation", help, StringComparison.Ordinal);
        Assert.Contains("--observability", help, StringComparison.Ordinal);
        Assert.Contains("--no-observability", help, StringComparison.Ordinal);
        Assert.Contains("--non-interactive", help, StringComparison.Ordinal);
        Assert.Empty(processes.Calls);
    }

    private static FakeProcessRunner TemplatePresentThenCreate()
    {
        FakeProcessRunner processes = new();
        processes.Enqueue(new ProcessResult(0, "Template Name Short Name Language\nSliceForge Minimal API sliceforge-api [C#]\n", string.Empty));
        processes.Enqueue(new ProcessResult(0, "Created.\n", string.Empty));
        return processes;
    }
}
