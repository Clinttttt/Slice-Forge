using FluentValidation;

namespace SliceForge.Validation.Routing;

internal sealed class ValidationRegistrationStore
{
    private readonly object _syncRoot = new();
    private Dictionary<Type, IValidationRoute> _routes = new();

    public void Add<TMessage, TValidator>()
        where TValidator : class, IValidator<TMessage>
    {
        Type messageType = typeof(TMessage);
        Type validatorType = typeof(TValidator);

        lock (_syncRoot)
        {
            if (_routes.TryGetValue(messageType, out IValidationRoute? existingRoute))
            {
                _routes[messageType] = existingRoute.AddValidator(validatorType);
                return;
            }

            _routes.Add(messageType, new ValidationRoute<TMessage>(validatorType));
        }
    }

    public void AddRange(IReadOnlyCollection<IValidationRoute> routes)
    {
        lock (_syncRoot)
        {
            Dictionary<Type, IValidationRoute> updatedRoutes = new(_routes);

            foreach (IValidationRoute discoveredRoute in routes)
            {
                if (!updatedRoutes.TryGetValue(
                        discoveredRoute.MessageType,
                        out IValidationRoute? existingRoute))
                {
                    updatedRoutes.Add(discoveredRoute.MessageType, discoveredRoute);
                    continue;
                }

                foreach (Type validatorType in discoveredRoute.ValidatorTypes)
                {
                    existingRoute = existingRoute.AddValidator(validatorType);
                }

                updatedRoutes[discoveredRoute.MessageType] = existingRoute;
            }

            _routes = updatedRoutes;
        }
    }

    public IReadOnlyDictionary<Type, IValidationRoute> Snapshot()
    {
        lock (_syncRoot)
        {
            return new Dictionary<Type, IValidationRoute>(_routes);
        }
    }
}
