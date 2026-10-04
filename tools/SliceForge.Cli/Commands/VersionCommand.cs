using SliceForge.Cli.Console;
using SliceForge.Cli.Templates;

namespace SliceForge.Cli.Commands;

internal sealed class VersionCommand(ICliConsole console)
{
    public async Task<int> ExecuteAsync()
    {
        await console.Output.WriteLineAsync(TemplateIdentity.Version).ConfigureAwait(false);
        return 0;
    }
}
