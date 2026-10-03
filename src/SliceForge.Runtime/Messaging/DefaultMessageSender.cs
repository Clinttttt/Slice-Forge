using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.Routing;

namespace SliceForge.Runtime.Messaging;

internal sealed class DefaultMessageSender : IMessageSender
{
    private readonly RouteCatalog _routeCatalog;
    private readonly IServiceProvider _serviceProvider;

    public DefaultMessageSender(RouteCatalog routeCatalog, IServiceProvider serviceProvider)
    {
        _routeCatalog = routeCatalog;
        _serviceProvider = serviceProvider;
    }

    public async Task<Result> SendCommandAsync(
        ICommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        IMessageRoute route = _routeCatalog.GetRequiredRoute(command.GetType());
        return await route.ExecuteAsync(command, _serviceProvider, cancellationToken);
    }

    public async Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        IMessageRoute route = _routeCatalog.GetRequiredRoute(command.GetType());
        Result result = await route.ExecuteAsync(command, _serviceProvider, cancellationToken);

        if (result is Result<TResponse> typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"The route for '{command.GetType().FullName}' returned an incompatible result type.");
    }

    public async Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        IMessageRoute route = _routeCatalog.GetRequiredRoute(query.GetType());
        Result result = await route.ExecuteAsync(query, _serviceProvider, cancellationToken);

        if (result is Result<TResponse> typedResult)
        {
            return typedResult;
        }

        throw new InvalidOperationException(
            $"The route for '{query.GetType().FullName}' returned an incompatible result type.");
    }
}
