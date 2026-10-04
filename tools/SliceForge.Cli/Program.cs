using SliceForge.Cli.Commands;
using SliceForge.Cli.Console;
using SliceForge.Cli.Processes;

namespace SliceForge.Cli;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        using CancellationTokenSource cancellationTokenSource = new();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellationTokenSource.Cancel();
        };

        System.Console.CancelKeyPress += cancelHandler;

        try
        {
            return await CliCommandFactory.InvokeAsync(
                args,
                SystemCliConsole.Instance,
                new DotnetProcessRunner(),
                cancellationTokenSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationTokenSource.IsCancellationRequested)
        {
            return 130;
        }
        finally
        {
            System.Console.CancelKeyPress -= cancelHandler;
        }
    }
}
