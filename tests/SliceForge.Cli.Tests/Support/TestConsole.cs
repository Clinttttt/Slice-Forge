using SliceForge.Cli.Console;

namespace SliceForge.Cli.Tests.Support;

internal sealed class TestConsole(string input = "", bool isInputRedirected = false, bool supportsUnicode = true) : ICliConsole
{
    private readonly CountingTextReader inputReader = new(input);

    public bool IsInputRedirected { get; } = isInputRedirected;

    public bool SupportsUnicode { get; } = supportsUnicode;

    public TextReader Input => inputReader;

    public int InputReadCount => inputReader.ReadCount;

    public StringWriter OutputWriter { get; } = new();

    public StringWriter ErrorWriter { get; } = new();

    public TextWriter Output => OutputWriter;

    public TextWriter Error => ErrorWriter;

    private sealed class CountingTextReader(string value) : StringReader(value)
    {
        public int ReadCount { get; private set; }

        public override ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            ReadCount++;
            return base.ReadLineAsync(cancellationToken);
        }
    }
}
