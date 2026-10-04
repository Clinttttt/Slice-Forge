using System.Text.RegularExpressions;
using SliceForge.Cli.Console;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Templates;

namespace SliceForge.Cli.Commands;

internal sealed class DoctorCommand(ICliConsole console, IProcessRunner processRunner)
{
    private static readonly Regex SdkVersionLine = new(
        "^\\s*(?<version>\\d+)\\.\\d+\\.\\d+",
        RegexOptions.CultureInvariant | RegexOptions.Multiline);

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken)
    {
        ProcessResult sdkResult = await processRunner.RunAsync("dotnet", ["--list-sdks"], cancellationToken).ConfigureAwait(false);
        bool sdkDetected = sdkResult.ExitCode == 0 && SdkVersionLine.IsMatch(sdkResult.StandardOutput);
        bool net10Detected = sdkDetected && SdkVersionLine.Matches(sdkResult.StandardOutput)
            .Any(match => match.Groups["version"].Value == "10");

        bool templateInstalled = false;
        if (sdkDetected)
        {
            templateInstalled = await new DotnetTemplateClient(processRunner)
                .IsInstalledAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await console.Output.WriteLineAsync("SliceForge Doctor").ConfigureAwait(false);
        await console.Output.WriteLineAsync().ConfigureAwait(false);
        await WriteCheckAsync(sdkDetected, ".NET SDK detected", ".NET SDK missing").ConfigureAwait(false);
        await WriteCheckAsync(net10Detected, ".NET 10 SDK detected", ".NET 10 SDK missing").ConfigureAwait(false);
        await WriteCheckAsync(templateInstalled, "SliceForge template installed", "SliceForge template not installed").ConfigureAwait(false);

        if (templateInstalled && net10Detected)
        {
            await console.Output.WriteLineAsync().ConfigureAwait(false);
            await console.Output.WriteLineAsync("Environment ready.").ConfigureAwait(false);
            return 0;
        }

        if (!templateInstalled)
        {
            await console.Output.WriteLineAsync().ConfigureAwait(false);
            await console.Output.WriteLineAsync("Install:").ConfigureAwait(false);
            await console.Output.WriteLineAsync(TemplateIdentity.InstallCommand).ConfigureAwait(false);
        }

        return 1;
    }

    private async Task WriteCheckAsync(bool passed, string success, string failure)
    {
        string symbol = console.SupportsUnicode
            ? passed ? "✓" : "✗"
            : passed ? "+" : "!";
        await console.Output.WriteLineAsync($"{symbol} {(passed ? success : failure)}").ConfigureAwait(false);
    }
}
