using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.TypeLoadDependency;

namespace SliceForge.Runtime.TypeLoadFixtures;

public sealed record UninspectableCommand : ICommand;

public sealed class UninspectableHandler : MissingFixtureBase, ICommandHandler<UninspectableCommand>
{
    public Task<Result> HandleAsync(UninspectableCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
