using Microsoft.Extensions.DependencyInjection;

namespace SliceForge.Runtime.Routing;

internal static class HandlerResolver
{
    public static THandler ResolveSingle<THandler>(
        IServiceProvider serviceProvider,
        Type messageType)
        where THandler : class
    {
        THandler[] handlers = serviceProvider.GetServices<THandler>().ToArray();

        return handlers.Length switch
        {
            0 => throw new InvalidOperationException(
                $"No handler is registered for '{messageType.FullName}'."),
            1 => handlers[0],
            _ => throw new InvalidOperationException(
                $"Multiple handlers are registered for '{messageType.FullName}'. Exactly one handler is required.")
        };
    }
}
