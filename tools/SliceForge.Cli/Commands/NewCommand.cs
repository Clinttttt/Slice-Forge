using System.Text.RegularExpressions;
using SliceForge.Cli.Console;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Templates;

namespace SliceForge.Cli.Commands;

internal sealed class NewCommand(ICliConsole console, DotnetTemplateClient templateClient)
{
    private static readonly Regex ValidProjectName = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant);

    public async Task<int> ExecuteAsync(string? name, string? outputDirectory, CancellationToken cancellationToken)
    {
        try
        {
            return await ExecuteCoreAsync(name, outputDirectory, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return 130;
        }
    }

    private async Task<int> ExecuteCoreAsync(string? name, string? outputDirectory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            if (console.IsInputRedirected)
            {
                await console.Error.WriteLineAsync("A project name is required when input is redirected.").ConfigureAwait(false);
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

        bool templateInstalled = await templateClient.IsInstalledAsync(cancellationToken).ConfigureAwait(false);
        if (!templateInstalled)
        {
            bool installConfirmed = await ConfirmTemplateInstallAsync(cancellationToken).ConfigureAwait(false);
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

        ProcessResult result = await templateClient.CreateAsync(name, outputDirectory, cancellationToken).ConfigureAwait(false);
        await WriteProcessOutputAsync(result).ConfigureAwait(false);
        return result.ExitCode;
    }

    private async Task<bool> ConfirmTemplateInstallAsync(CancellationToken cancellationToken)
    {
        if (console.IsInputRedirected)
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

        return string.IsNullOrWhiteSpace(answer) ||
               string.Equals(answer.Trim(), "y", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(answer.Trim(), "yes", StringComparison.OrdinalIgnoreCase);
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
