using SliceForge.Results;

namespace SliceForge.Validation.Routing;

internal interface IValidationRoute
{
    Type MessageType { get; }

    IReadOnlyList<Type> ValidatorTypes { get; }

    IValidationRoute AddValidator(Type validatorType);

    Task<Error?> ValidateAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
