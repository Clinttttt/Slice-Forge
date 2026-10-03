using SliceForge.Results;

namespace SliceForge.Runtime.Routing;

internal interface IMessageRoute
{
    Type MessageType { get; }

    Task<Result> ExecuteAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
