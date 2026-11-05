using System.Text.RegularExpressions;
using SliceForge.Cli.Console;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Templates;

namespace SliceForge.Cli.Commands;

internal sealed class NewCommand(ICliConsole console, DotnetTemplateClient templateClient)
{
    private static readonly Regex ValidProjectName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public async Task<int> ExecuteAsync(
        string? name,
        string? outputDirectory,
        bool validation,
        bool noValidation,
        bool observability,
        bool noObservability,
        bool nonInteractive,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(
                name,
                outputDirectory,
                validation,
                noValidation,
                observability,
                noObservability,
                nonInteractive,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
    }

    private async Task<int> ExecuteCoreAsync(
        string? name,
        string? outputDirectory,
        bool validation,
        bool noValidation,
        bool observability,
        bool noObservability,
        bool nonInteractive,
        CancellationToken cancellationToken)
    {
        if (validation && noValidation)
        {
            await console.Error.WriteLineAsync("Choose either --validation or --no-validation, not both.").ConfigureAwait(false);
            return 2;
        }

        if (observability && noObservability)
        {
            await console.Error.WriteLineAsync("Choose either --observability or --no-observability, not both.").ConfigureAwait(false);
            return 2;
        }

        bool hasExplicitCapabilities = validation || noValidation || observability || noObservability;
        bool canPrompt = !console.IsInputRedirected && !nonInteractive && !hasExplicitCapabilities;

        if (string.IsNullOrWhiteSpace(name))
        {
            if (!canPrompt)
            {
                await console.Error.WriteLineAsync("A project name is required when input is redirected or prompts are disabled.").ConfigureAwait(false);
                await console.Error.WriteLineAsync("Usage: sliceforge new --name <project-name>").ConfigureAwait(false);
                return 2;
            }

            await console.Output.WriteAsync("Project name: ").ConfigureAwait(false);
            await console.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
            name = await console.Input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            await console.Error.WriteLineAsync("A project name is required.").ConfigureAwait(false);
            return 2;
        }

        if (!ValidProjectName.IsMatch(name))
        {
            await console.Error.WriteLineAsync("Use a project name that starts with a letter or underscore and contains only letters, digits, and underscores.").ConfigureAwait(false);
            return 2;
        }

        bool useValidation = !noValidation;
        bool useObservability = !noObservability;
        bool promptForCapabilities = canPrompt;

        if (promptForCapabilities)
        {
            await console.Output.WriteLineAsync("SliceForge - New Application").ConfigureAwait(false);
            await console.Output.WriteLineAsync().ConfigureAwait(false);
            await console.Output.WriteLineAsync("Project capabilities").ConfigureAwait(false);
            await console.Output.WriteLineAsync().ConfigureAwait(false);

            bool? validationChoice = await PromptYesByDefaultAsync("  Use validation? [Y/n] ", cancellationToken).ConfigureAwait(false);
            if (validationChoice is null)
            {
                await console.Error.WriteLineAsync("Project creation was not confirmed.").ConfigureAwait(false);
                return 1;
            }

            bool? observabilityChoice = await PromptYesByDefaultAsync("  Use observability? [Y/n] ", cancellationToken).ConfigureAwait(false);
            if (observabilityChoice is null)
            {
                await console.Error.WriteLineAsync("Project creation was not confirmed.").ConfigureAwait(false);
                return 1;
            }

            useValidation = validationChoice.Value;
            useObservability = observabilityChoice.Value;
            await WriteSelectionSummaryAsync(name, outputDirectory, useValidation, useObservability).ConfigureAwait(false);

            bool? createProject = await PromptYesByDefaultAsync("Create project? [Y/n] ", cancellationToken).ConfigureAwait(false);
            if (createProject is null || !createProject.Value)
            {
                await console.Error.WriteLineAsync("Project creation was not confirmed.").ConfigureAwait(false);
                return 1;
            }
        }

        bool templateInstalled = await templateClient.IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        if (!templateInstalled)
        {
            bool installConfirmed = await ConfirmTemplateInstallAsync(canPrompt, cancellationToken).ConfigureAwait(false);
            if (!installConfirmed)
            {
                return 1;
            }

            ProcessResult installResult = await templateClient.InstallAsync(cancellationToken).ConfigureAwait(false);
            await WriteProcessOutputAsync(installResult).ConfigureAwait(false);
            if (installResult.ExitCode != 0)
            {
                return installResult.ExitCode;
            }
        }

        bool forwardCapabilities = promptForCapabilities || hasExplicitCapabilities;
        ProcessResult result = await templateClient.CreateAsync(
            name,
            outputDirectory,
            forwardCapabilities ? useValidation : null,
            forwardCapabilities ? useObservability : null,
            cancellationToken).ConfigureAwait(false);
        await WriteProcessOutputAsync(result).ConfigureAwait(false);
        return result.ExitCode;
    }

    private async Task<bool?> PromptYesByDefaultAsync(string prompt, CancellationToken cancellationToken)
    {
        while (true)
        {
            await console.Output.WriteAsync(prompt).ConfigureAwait(false);
            await console.Output.FlushAsync(cancellationToken).ConfigureAwait(false);
            string? answer = await console.Input.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (answer is null)
            {
                return null;
            }

            string normalized = answer.Trim();
            if (normalized.Length == 0 ||
                string.Equals(normalized, "y", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "yes", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (string.Equals(normalized, "n", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalized, "no", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            await console.Output.WriteLineAsync("Please answer yes or no.").ConfigureAwait(false);
        }
    }

    private async Task WriteSelectionSummaryAsync(
        string name,
        string? outputDirectory,
        bool useValidation,
        bool useObservability)
    {
        await console.Output.WriteLineAsync().ConfigureAwait(false);
        await console.Output.WriteLineAsync($"Project: {name}").ConfigureAwait(false);
        await console.Output.WriteLineAsync("Framework: .NET 10").ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            await console.Output.WriteLineAsync($"Output: {outputDirectory}").ConfigureAwait(false);
        }

        await console.Output.WriteLineAsync().ConfigureAwait(false);
        await console.Output.WriteLineAsync("Selected packages:").ConfigureAwait(false);
        await console.Output.WriteLineAsync("  SliceForge.Core").ConfigureAwait(false);
        await console.Output.WriteLineAsync("  SliceForge.Runtime").ConfigureAwait(false);
        await console.Output.WriteLineAsync("  SliceForge.AspNetCore").ConfigureAwait(false);
        if (useValidation)
        {
            await console.Output.WriteLineAsync("  SliceForge.Validation").ConfigureAwait(false);
            await console.Output.WriteLineAsync("  FluentValidation (used directly by the generated validator)").ConfigureAwait(false);
        }

        if (useObservability)
        {
            await console.Output.WriteLineAsync("  SliceForge.Observability").ConfigureAwait(false);
        }

        await console.Output.WriteLineAsync($"Validation: {(useValidation ? "Yes" : "No")}").ConfigureAwait(false);
        await console.Output.WriteLineAsync($"Observability: {(useObservability ? "Yes" : "No")} (instrumentation only; no exporter is configured)").ConfigureAwait(false);
        await console.Output.WriteLineAsync().ConfigureAwait(false);
    }

    private async Task<bool> ConfirmTemplateInstallAsync(bool allowPrompt, CancellationToken cancellationToken)
    {
        if (!allowPrompt)
        {
            await console.Error.WriteLineAsync("SliceForge.Templates is required.").ConfigureAwait(false);
            await console.Error.WriteLineAsync().ConfigureAwait(false);
            await console.Error.WriteLineAsync("Install:").ConfigureAwait(false);
            await console.Error.WriteLineAsync(TemplateIdentity.InstallCommand).ConfigureAwait(false);
            return false;
        }

        await console.Error.WriteLineAsync($"{TemplateIdentity.PackageId} {TemplateIdentity.Version} is not installed.").ConfigureAwait(false);
        await console.Error.WriteAsync("Install it now? [Y/n] ").ConfigureAwait(false);
        await console.Error.FlushAsync(cancellationToken).ConfigureAwait(false);
        string? answer = await console.Input.ReadLineAsync(cancellationToken).ConfigureAwait(false);

        return answer is not null && (string.IsNullOrWhiteSpace(answer) ||
               string.Equals(answer.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer.Trim(), "yes", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WriteProcessOutputAsync(ProcessResult result)
    {
        if (!string.IsNullOrEmpty(result.StandardOutput))
        {
            await console.Output.WriteAsync(result.StandardOutput).ConfigureAwait(false);
        }

        if (!string.IsNullOrEmpty(result.StandardError))
        {
            await console.Error.WriteAsync(result.StandardError).ConfigureAwait(false);
        }
    }
}
