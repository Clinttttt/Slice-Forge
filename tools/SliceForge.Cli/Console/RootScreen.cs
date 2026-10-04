namespace SliceForge.Cli.Console;

internal static class RootScreen
{
    private const int ContentWidth = 40;

    public static void Write(TextWriter output, string version, bool supportsUnicode)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(version);

        char horizontal = supportsUnicode ? '─' : '-';
        char vertical = supportsUnicode ? '│' : '|';
        char topLeft = supportsUnicode ? '╭' : '+';
        char topRight = supportsUnicode ? '╮' : '+';
        char bottomLeft = supportsUnicode ? '╰' : '+';
        char bottomRight = supportsUnicode ? '╯' : '+';

        output.WriteLine($"{topLeft}{new string(horizontal, ContentWidth + 2)}{topRight}");
        WriteLine(output, vertical, "SliceForge");
        WriteLine(output, vertical, "Vertical Slice Toolkit for .NET 10");
        WriteLine(output, vertical, $"v{version}");
        WriteLine(output, vertical, "Author: Clint Villanueva");
        output.WriteLine($"{bottomLeft}{new string(horizontal, ContentWidth + 2)}{bottomRight}");
        output.WriteLine();
        output.WriteLine("new       Create a SliceForge application");
        output.WriteLine("doctor    Check the environment");
        output.WriteLine("version   Show version");
    }

    private static void WriteLine(TextWriter output, char vertical, string value)
    {
        output.WriteLine($"{vertical} {value.PadRight(ContentWidth)} {vertical}");
    }
}
