namespace SliceForge.Cli.Console;

internal interface ICliConsole
{
    bool IsInputRedirected { get; }

    bool SupportsUnicode { get; }

    TextReader Input { get; }

    TextWriter Output { get; }

    TextWriter Error { get; }
}
