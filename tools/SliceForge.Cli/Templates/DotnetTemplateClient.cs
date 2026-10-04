using SliceForge.Cli.Processes;

namespace SliceForge.Cli.Templates;

internal sealed class DotnetTemplateClient(IProcessRunner processRunner)
{
    public async Task<bool> IsInstalledAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await processRunner.RunAsync(
            "dotnet",
            ["new", "list", TemplateIdentity.ShortName],
            cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            return false;
        }

        return result.StandardOutput
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .Any(line => line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
                .Contains(TemplateIdentity.ShortName, StringComparer.OrdinalIgnoreCase));
    }

    public Task<ProcessResult> InstallAsync(CancellationToken cancellationToken) =>
        processRunner.RunAsync(
            "dotnet",
            ["new", "install", $"{TemplateIdentity.PackageId}@{TemplateIdentity.Version}"],
            cancellationToken);

    public Task<ProcessResult> CreateAsync(
        string name,
        string? outputDirectory,
        CancellationToken cancellationToken)
    {
        List<string> arguments = ["new", TemplateIdentity.ShortName, "-n", name];

        if (!string.IsNullOrWhiteSpace(outputDirectory))
        {
            arguments.Add("--output");
            arguments.Add(outputDirectory);
        }

        return processRunner.RunAsync("dotnet", arguments, cancellationToken);
    }
}
