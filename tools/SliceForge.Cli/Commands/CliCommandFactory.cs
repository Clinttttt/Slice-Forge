using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using SliceForge.Cli.Console;
using SliceForge.Cli.Processes;
using SliceForge.Cli.Templates;

namespace SliceForge.Cli.Commands;

internal static class CliCommandFactory
{
    public static RootCommand Create(ICliConsole console, IProcessRunner processRunner)
    {
        ArgumentNullException.ThrowIfNull(console);
        ArgumentNullException.ThrowIfNull(processRunner);

        RootCommand root = new("SliceForge command-line toolkit.");
        VersionOption versionOption = root.Options.OfType<VersionOption>().Single();
        versionOption.Action = new WriteVersionAction(TemplateIdentity.Version);
        root.SetAction(_ =>
        {
            RootScreen.Write(console.Output, TemplateIdentity.Version, console.SupportsUnicode);
            return 0;
        });

        Command versionCommand = new("version", "Show version");
        versionCommand.SetAction(async _ => await new VersionCommand(console).ExecuteAsync().ConfigureAwait(false));

        Command doctorCommand = new("doctor", "Check the environment");
        doctorCommand.SetAction(async (_, cancellationToken) =>
            await new DoctorCommand(console, processRunner).ExecuteAsync(cancellationToken).ConfigureAwait(false));

        Option<string?> nameOption = new("--name", "-n")
        {
            Description = "Application name"
        };
        Option<string?> outputOption = new("--output", "-o")
        {
            Description = "Output directory"
        };
        Command newCommand = new("new", "Create a SliceForge application");
        newCommand.Options.Add(nameOption);
        newCommand.Options.Add(outputOption);
        newCommand.SetAction(async (parseResult, cancellationToken) =>
            await new NewCommand(console, new DotnetTemplateClient(processRunner))
                .ExecuteAsync(
                    parseResult.GetValue(nameOption),
                    parseResult.GetValue(outputOption),
                    cancellationToken)
                .ConfigureAwait(false));

        root.Subcommands.Add(newCommand);
        root.Subcommands.Add(doctorCommand);
        root.Subcommands.Add(versionCommand);

        return root;
    }

    private sealed class WriteVersionAction(string version) : SynchronousCommandLineAction
    {
        public override int Invoke(ParseResult parseResult)
        {
            parseResult.InvocationConfiguration.Output.WriteLine(version);
            return 0;
        }

        public override bool ClearsParseErrors => true;
    }

    public static Task<int> InvokeAsync(
        string[] args,
        ICliConsole console,
        IProcessRunner processRunner,
        CancellationToken cancellationToken = default)
    {
        RootCommand root = Create(console, processRunner);
        InvocationConfiguration configuration = new()
        {
            Output = console.Output,
            Error = console.Error
        };

        return root.Parse(args).InvokeAsync(configuration, cancellationToken);
    }
}
