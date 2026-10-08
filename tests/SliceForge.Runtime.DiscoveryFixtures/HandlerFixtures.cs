using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.DiscoveryFixtures;

public sealed class DiscoveryProbe
{
    public Result NonGenericResult { get; set; } = Result.Success();

    public Result<Guid> TypedResult { get; set; } = Result<Guid>.Success(Guid.Empty);

    public int NonGenericCalls { get; private set; }

    public int TypedCalls { get; private set; }

    public int QueryCalls { get; private set; }

    public int FirstMultiContractCalls { get; private set; }

    public int SecondMultiContractCalls { get; private set; }

    public CancellationToken ReceivedCancellationToken { get; private set; }

    public void RecordNonGeneric(CancellationToken cancellationToken)
    {
        NonGenericCalls++;
        ReceivedCancellationToken = cancellationToken;
    }

    public void RecordTyped(CancellationToken cancellationToken)
    {
        TypedCalls++;
        ReceivedCancellationToken = cancellationToken;
    }

    public void RecordQuery(CancellationToken cancellationToken)
    {
        QueryCalls++;
        ReceivedCancellationToken = cancellationToken;
    }

    public void RecordFirstMultiContract() => FirstMultiContractCalls++;

    public void RecordSecondMultiContract() => SecondMultiContractCalls++;
}

public sealed record DiscoveredDeleteCommand : ICommand;

public sealed record DiscoveredCreateCommand : ICommand<Guid>;

public sealed record DiscoveredEchoQuery(string Value) : IQuery<string>;

public sealed record FirstMultiContractCommand : ICommand;

public sealed record SecondMultiContractCommand : ICommand;

public record DiscoveredBaseCommand : ICommand;

public sealed record DiscoveredDerivedCommand : DiscoveredBaseCommand;

public sealed record AbstractOnlyCommand : ICommand;

public sealed record OpenGenericCommand<TValue>(TValue Value) : ICommand;

internal sealed class DiscoveredDeleteHandler(DiscoveryProbe probe) : ICommandHandler<DiscoveredDeleteCommand>
{
    public Task<Result> HandleAsync(DiscoveredDeleteCommand command, CancellationToken cancellationToken)
    {
        probe.RecordNonGeneric(cancellationToken);
        return Task.FromResult(probe.NonGenericResult);
    }
}

internal sealed class DiscoveredCreateHandler(DiscoveryProbe probe) : ICommandHandler<DiscoveredCreateCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(DiscoveredCreateCommand command, CancellationToken cancellationToken)
    {
        probe.RecordTyped(cancellationToken);
        return Task.FromResult(probe.TypedResult);
    }
}

internal sealed class DiscoveredEchoHandler(DiscoveryProbe probe) : IQueryHandler<DiscoveredEchoQuery, string>
{
    public Task<Result<string>> HandleAsync(DiscoveredEchoQuery query, CancellationToken cancellationToken)
    {
        probe.RecordQuery(cancellationToken);
        return Task.FromResult(Result<string>.Success(query.Value));
    }
}

internal sealed class MultiContractHandler(DiscoveryProbe probe) :
    ICommandHandler<FirstMultiContractCommand>,
    ICommandHandler<SecondMultiContractCommand>
{
    public Task<Result> HandleAsync(FirstMultiContractCommand command, CancellationToken cancellationToken)
    {
        probe.RecordFirstMultiContract();
        return Task.FromResult(Result.Success());
    }

    public Task<Result> HandleAsync(SecondMultiContractCommand command, CancellationToken cancellationToken)
    {
        probe.RecordSecondMultiContract();
        return Task.FromResult(Result.Success());
    }
}

internal sealed class DiscoveredBaseHandler : ICommandHandler<DiscoveredBaseCommand>
{
    public Task<Result> HandleAsync(DiscoveredBaseCommand command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal abstract class AbstractFixtureHandler : ICommandHandler<AbstractOnlyCommand>
{
    public abstract Task<Result> HandleAsync(AbstractOnlyCommand command, CancellationToken cancellationToken);
}

internal sealed class OpenGenericFixtureHandler<TValue> : ICommandHandler<OpenGenericCommand<TValue>>
{
    public Task<Result> HandleAsync(OpenGenericCommand<TValue> command, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}

internal sealed class UnrelatedFixtureType;
