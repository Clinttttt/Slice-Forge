using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.Routing;

internal sealed class CommandRoute<TCommand> : IMessageRoute
    where TCommand : ICommand
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

        ICommandHandler<TCommand> handler = HandlerResolver.ResolveSingle<ICommandHandler<TCommand>>(
            serviceProvider,
            MessageType);

        Result result = await handler.HandleAsync(command, cancellationToken);

        if (result is null)
        {
            throw new InvalidOperationException(
                $"The handler for '{MessageType.FullName}' returned a null result.");
        }

        return result;
    }
}
