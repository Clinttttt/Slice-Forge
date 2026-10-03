using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.Routing;

internal sealed class QueryRoute<TQuery, TResponse> : IMessageRoute
    where TQuery : IQuery<TResponse>
{
    public Type MessageType => typeof(TQuery);

    public async Task<Result> ExecuteAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (message is not TQuery query)
        {
            throw new InvalidOperationException(
                $"The message runtime type does not match the registered route for '{MessageType.FullName}'.");
        }

        IQueryHandler<TQuery, TResponse> handler = HandlerResolver.ResolveSingle<
            IQueryHandler<TQuery, TResponse>>(serviceProvider, MessageType);

        Result<TResponse> result = await handler.HandleAsync(query, cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                $"The handler for '{MessageType.FullName}' returned a null result.");
        }

        return result;
    }
}
