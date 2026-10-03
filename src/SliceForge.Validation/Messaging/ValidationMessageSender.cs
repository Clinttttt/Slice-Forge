using SliceForge.Messaging;
using SliceForge.Results;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.Routing;

namespace SliceForge.Validation.Messaging;

internal sealed class ValidationMessageSender : IMessageSender
{
    private readonly IMessageSender _innerSender;
    private readonly ValidationRouteCatalog _routeCatalog;
    private readonly IServiceProvider _serviceProvider;

    public ValidationMessageSender(
        IMessageSender innerSender,
        ValidationRouteCatalog routeCatalog,
        IServiceProvider serviceProvider)
    {
        _innerSender = innerSender;
        _routeCatalog = routeCatalog;
        _serviceProvider = serviceProvider;
    }

    public async Task<Result> SendCommandAsync(
        ICommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Error? validationError = await ValidateAsync(command, cancellationToken);
        return validationError is null
            ? await _innerSender.SendCommandAsync(command, cancellationToken)
            : Result.Failure(validationError);
    }

    public async Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        Error? validationError = await ValidateAsync(command, cancellationToken);
        return validationError is null
            ? await _innerSender.SendCommandAsync(command, cancellationToken)
            : Result<TResponse>.Failure(validationError);
    }

    public async Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        Error? validationError = await ValidateAsync(query, cancellationToken);
        return validationError is null
            ? await _innerSender.SendQueryAsync(query, cancellationToken)
            : Result<TResponse>.Failure(validationError);
    }

    private Task<Error?> ValidateAsync(object message, CancellationToken cancellationToken)
    {
        IValidationRoute? route = _routeCatalog.Find(message.GetType());
        return route is null
            ? Task.FromResult<Error?>(null)
            : route.ValidateAsync(message, _serviceProvider, cancellationToken);
    }
}
