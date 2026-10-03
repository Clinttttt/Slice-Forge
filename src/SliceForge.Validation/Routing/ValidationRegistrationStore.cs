using FluentValidation;

namespace SliceForge.Validation.Routing;

internal sealed class ValidationRegistrationStore
{
    private readonly object _syncRoot = new();
    private readonly Dictionary<Type, IValidationRoute> _routes = new();

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

    public IReadOnlyDictionary<Type, IValidationRoute> Snapshot()
    {
        lock (_syncRoot)
        {
            return new Dictionary<Type, IValidationRoute>(_routes);
        }
    }
}
