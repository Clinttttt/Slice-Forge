using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.Routing;

internal sealed class TypedCommandRoute<TCommand, TResponse> : IMessageRoute
    where TCommand : ICommand<TResponse>
{
    public Type MessageType => typeof(TCommand);

    public async Task<Result> ExecuteAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (message is not TCommand command)
        {
            throw new InvalidOperationException(
                $"The message runtime type does not match the registered route for '{MessageType.FullName}'.");
        }

        ICommandHandler<TCommand, TResponse> handler = HandlerResolver.ResolveSingle<
            ICommandHandler<TCommand, TResponse>>(serviceProvider, MessageType);

        Result<TResponse> result = await handler.HandleAsync(command, cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                $"The handler for '{MessageType.FullName}' returned a null result.");
        }

        return result;
    }
}
