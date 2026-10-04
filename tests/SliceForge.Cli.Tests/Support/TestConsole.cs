using SliceForge.Cli.Console;

namespace SliceForge.Cli.Tests.Support;

internal sealed class TestConsole(string input = "", bool isInputRedirected = false, bool supportsUnicode = true) : ICliConsole
{
    public bool IsInputRedirected { get; } = isInputRedirected;

    public bool SupportsUnicode { get; } = supportsUnicode;

    public TextReader Input { get; } = new StringReader(input);

    public StringWriter OutputWriter { get; } = new();

    public StringWriter ErrorWriter { get; } = new();

    public TextWriter Output => OutputWriter;

    public TextWriter Error => ErrorWriter;
}
