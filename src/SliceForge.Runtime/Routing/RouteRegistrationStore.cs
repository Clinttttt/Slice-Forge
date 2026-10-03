namespace SliceForge.Runtime.Routing;

internal sealed class RouteRegistrationStore
{
    private readonly Dictionary<Type, IMessageRoute> _routes = new();

    public void Add(IMessageRoute route)
    {
        if (!_routes.TryAdd(route.MessageType, route))
        {
            throw new InvalidOperationException(
                $"A message route for '{route.MessageType.FullName}' is already registered.");
        }
    }

    public IReadOnlyCollection<IMessageRoute> Snapshot() => _routes.Values.ToArray();
}
