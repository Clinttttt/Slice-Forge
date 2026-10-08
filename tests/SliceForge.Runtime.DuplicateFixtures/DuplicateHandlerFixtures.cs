using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.DuplicateFixtures;

public sealed record DuplicateCommand : ICommand;

public sealed record DuplicateQuery : IQuery<string>;

public sealed record CommandQueryCollision : ICommand, IQuery<string>;

internal sealed class FirstDuplicateCommandHandler : ICommandHandler<DuplicateCommand>
{
    public Task<Result> HandleAsync(DuplicateCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class SecondDuplicateCommandHandler : ICommandHandler<DuplicateCommand>
{
    public Task<Result> HandleAsync(DuplicateCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class FirstDuplicateQueryHandler : IQueryHandler<DuplicateQuery, string>
{
    public Task<Result<string>> HandleAsync(DuplicateQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string>.Success("first"));
}

internal sealed class SecondDuplicateQueryHandler : IQueryHandler<DuplicateQuery, string>
{
    public Task<Result<string>> HandleAsync(DuplicateQuery query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string>.Success("second"));
}

internal sealed class CollisionCommandHandler : ICommandHandler<CommandQueryCollision>
{
    public Task<Result> HandleAsync(CommandQueryCollision command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class CollisionQueryHandler : IQueryHandler<CommandQueryCollision, string>
{
    public Task<Result<string>> HandleAsync(CommandQueryCollision query, CancellationToken cancellationToken) =>
        Task.FromResult(Result<string>.Success("collision"));
}
