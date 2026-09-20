using System.Threading;
using System.Threading.Tasks;
using SliceForge.Results;

namespace SliceForge.Messaging;

/// <summary>
/// Handles a command without a value payload.
/// </summary>
/// <typeparam name="TCommand">The command type handled by this handler.</typeparam>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    /// <summary>
    /// Handles the command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The command result.</returns>
    Task<Result> HandleAsync(TCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a command with a typed value payload.
/// </summary>
/// <typeparam name="TCommand">The command type handled by this handler.</typeparam>
/// <typeparam name="TResponse">The successful value type produced by the command.</typeparam>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    /// <summary>
    /// Handles the command asynchronously.
    /// </summary>
    /// <param name="command">The command to handle.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The command result containing the response value when successful.</returns>
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken cancellationToken);
}
