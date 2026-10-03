namespace SliceForge.Runtime.Routing;

internal sealed class RouteCatalog
{
    private readonly IReadOnlyDictionary<Type, IMessageRoute> _routes;

    public RouteCatalog(IEnumerable<IMessageRoute> routes)
    {
        Dictionary<Type, IMessageRoute> routeMap = new();

        foreach (IMessageRoute route in routes)
        {
            if (!routeMap.TryAdd(route.MessageType, route))
            {
                throw new InvalidOperationException(
                    $"A message route for '{route.MessageType.FullName}' is registered more than once.");
            }
        }

        _routes = routeMap;
    }

    public IMessageRoute GetRequiredRoute(Type messageType)
    {
        if (_routes.TryGetValue(messageType, out IMessageRoute? route))
        {
            return route;
        }

        throw new InvalidOperationException(
            $"No message route is registered for '{messageType.FullName}'.");
    }
}
