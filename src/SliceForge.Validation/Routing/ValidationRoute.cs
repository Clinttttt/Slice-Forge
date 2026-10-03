using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.Results;

namespace SliceForge.Validation.Routing;

internal sealed class ValidationRoute<TMessage> : IValidationRoute
{
    private readonly IReadOnlyList<Type> _validatorTypes;

    public ValidationRoute(Type validatorType)
    {
        MessageType = typeof(TMessage);
        _validatorTypes = new[] { validatorType };
    }

    private ValidationRoute(IReadOnlyList<Type> validatorTypes)
    {
        MessageType = typeof(TMessage);
        _validatorTypes = validatorTypes;
    }

    public Type MessageType { get; }

    public IValidationRoute AddValidator(Type validatorType)
    {
        if (_validatorTypes.Contains(validatorType))
        {
            throw new InvalidOperationException(
                $"Validator '{validatorType.FullName}' is already registered for " +
                $"message '{MessageType.FullName}'.");
        }

        List<Type> validatorTypes = new(_validatorTypes)
        {
            validatorType
        };

        return new ValidationRoute<TMessage>(validatorTypes.AsReadOnly());
    }

    public async Task<Error?> ValidateAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        if (message is not TMessage typedMessage)
        {
            throw new InvalidOperationException(
                $"The validation route for '{MessageType.FullName}' received an incompatible message.");
        }

        Dictionary<string, List<string>> failuresByProperty = new(StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> seenMessagesByProperty =
            new(StringComparer.Ordinal);

        foreach (Type validatorType in _validatorTypes)
        {
            object validatorObject = serviceProvider.GetRequiredService(validatorType);

            if (validatorObject is not IValidator<TMessage> validator)
            {
                throw new InvalidOperationException(
                    $"Validator '{validatorType.FullName}' is not an IValidator<{MessageType.FullName}>.");
            }

            ValidationResult validationResult =
                await validator.ValidateAsync(typedMessage, cancellationToken);

            foreach (ValidationFailure failure in validationResult.Errors)
            {
                string propertyName = failure.PropertyName ?? string.Empty;

                if (!failuresByProperty.TryGetValue(propertyName, out List<string>? messages))
                {
                    messages = new List<string>();
                    failuresByProperty.Add(propertyName, messages);
                    seenMessagesByProperty.Add(propertyName, new HashSet<string>(StringComparer.Ordinal));
                }

                if (seenMessagesByProperty[propertyName].Add(failure.ErrorMessage))
                {
                    messages.Add(failure.ErrorMessage);
                }
            }
        }

        if (failuresByProperty.Count == 0)
        {
            return null;
        }

        Dictionary<string, IReadOnlyList<string>> validationErrors = new(StringComparer.Ordinal);

        foreach ((string propertyName, List<string> messages) in failuresByProperty)
        {
            validationErrors.Add(propertyName, messages.ToArray());
        }

        return new Error(
            "Validation.Failed",
            "One or more validation errors occurred.",
            ErrorType.Validation,
            validationErrors);
    }
}
