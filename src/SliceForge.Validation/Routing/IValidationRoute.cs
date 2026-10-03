using SliceForge.Results;

namespace SliceForge.Validation.Routing;

internal interface IValidationRoute
{
    Type MessageType { get; }

    IValidationRoute AddValidator(Type validatorType);

    Task<Error?> ValidateAsync(
        object message,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}
