using SliceForge.Messaging;
using SliceForge.Results;

namespace SliceForge.Runtime.Messaging;

/// <summary>
/// Sends registered commands and queries to their handlers.
/// </summary>
public interface IMessageSender
{
    /// <summary>Sends a command that does not produce a value payload.</summary>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The command result.</returns>
    Task<Result> SendCommandAsync(ICommand command, CancellationToken cancellationToken);

    /// <summary>Sends a command that produces a value payload.</summary>
    /// <typeparam name="TResponse">The successful value type produced by the command.</typeparam>
    /// <param name="command">The command to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The command result containing the response value when successful.</returns>
    Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken);

    /// <summary>Sends a query that produces a value payload.</summary>
    /// <typeparam name="TResponse">The successful value type produced by the query.</typeparam>
    /// <param name="query">The query to send.</param>
    /// <param name="cancellationToken">The token used to observe cancellation.</param>
    /// <returns>The query result containing the response value when successful.</returns>
    Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken);
}
