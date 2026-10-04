using SliceForge.Cli.Processes;

namespace SliceForge.Cli.Tests.Support;

internal sealed class FakeProcessRunner : IProcessRunner
{
    private readonly Queue<Func<IReadOnlyList<string>, CancellationToken, Task<ProcessResult>>> responses = new();

    public List<IReadOnlyList<string>> Calls { get; } = [];

    public List<CancellationToken> CancellationTokens { get; } = [];

    public void Enqueue(ProcessResult result)
    {
        responses.Enqueue((_, _) => Task.FromResult(result));
    }

    public void Enqueue(Func<IReadOnlyList<string>, CancellationToken, Task<ProcessResult>> response)
    {
        responses.Enqueue(response);
    }

    public Task<ProcessResult> RunAsync(string fileName, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        Assert.Equal("dotnet", fileName);
        Calls.Add(arguments.ToArray());
        CancellationTokens.Add(cancellationToken);

        if (responses.Count == 0)
        {
            throw new InvalidOperationException("No fake process response was queued.");
        }

        return responses.Dequeue()(arguments, cancellationToken);
    }
}
