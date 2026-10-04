using System.Text;

namespace SliceForge.Cli.Console;

internal sealed class SystemCliConsole : ICliConsole
{
    public static SystemCliConsole Instance { get; } = new();

    private SystemCliConsole()
    {
    }

    public bool IsInputRedirected => System.Console.IsInputRedirected;

    public bool SupportsUnicode =>
        !System.Console.IsOutputRedirected &&
        System.Console.OutputEncoding.CodePage == Encoding.UTF8.CodePage &&
        !string.Equals(Environment.GetEnvironmentVariable("TERM"), "dumb", StringComparison.OrdinalIgnoreCase);

    public TextReader Input => System.Console.In;

    public TextWriter Output => System.Console.Out;

    public TextWriter Error => System.Console.Error;
}
