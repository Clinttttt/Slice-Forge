namespace SliceForge.Validation.Routing;

internal sealed class ValidationRouteCatalog
{
    private readonly IReadOnlyDictionary<Type, IValidationRoute> _routes;

    public ValidationRouteCatalog(IReadOnlyDictionary<Type, IValidationRoute> routes)
    {
        _routes = new Dictionary<Type, IValidationRoute>(routes);
    }

    public IValidationRoute? Find(Type messageType) =>
        _routes.TryGetValue(messageType, out IValidationRoute? route) ? route : null;
}
