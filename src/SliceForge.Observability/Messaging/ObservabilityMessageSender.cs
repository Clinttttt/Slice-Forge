using System.Diagnostics;
using Microsoft.Extensions.Logging;
using SliceForge.Messaging;
using SliceForge.Observability.Instrumentation;
using SliceForge.Results;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Observability.Messaging;

internal sealed class ObservabilityMessageSender : IMessageSender
{
    private readonly IMessageSender _innerSender;
    private readonly ILogger _logger;

    public ObservabilityMessageSender(IMessageSender innerSender, ILogger logger)
    {
        _innerSender = innerSender;
        _logger = logger;
    }

    public Task<Result> SendCommandAsync(ICommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ObserveAsync(
            command,
            "command",
            "sliceforge.command",
            () => _innerSender.SendCommandAsync(command, cancellationToken));
    }

    public Task<Result<TResponse>> SendCommandAsync<TResponse>(
        ICommand<TResponse> command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return ObserveAsync(
            command,
            "command",
            "sliceforge.command",
            () => _innerSender.SendCommandAsync(command, cancellationToken));
    }

    public Task<Result<TResponse>> SendQueryAsync<TResponse>(
        IQuery<TResponse> query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        return ObserveAsync(
            query,
            "query",
            "sliceforge.query",
            () => _innerSender.SendQueryAsync(query, cancellationToken));
    }

    private async Task<TResult> ObserveAsync<TResult>(
        object message,
        string messageKind,
        string activityName,
        Func<Task<TResult>> send)
        where TResult : Result
    {
        long startTimestamp = Stopwatch.GetTimestamp();
        string messageType = message.GetType().FullName ?? message.GetType().Name;
        Activity? activity = MessageInstrumentation.TryStartActivity(activityName, messageKind, messageType);
        string outcome = "exception";

        try
        {
            TResult result;

            try
            {
                result = await send();
            }
            catch (OperationCanceledException)
            {
                outcome = "cancelled";
                MessageInstrumentation.TryCompleteActivity(activity, outcome, errorType: null);
                TryLog(messageKind, messageType, outcome, errorType: null);
                throw;
            }
            catch (Exception)
            {
                outcome = "exception";
                MessageInstrumentation.TryCompleteActivity(activity, outcome, errorType: null);
                throw;
            }

            string? errorType = result.Error?.Type.ToString();
            outcome = result.IsSuccess
                ? "success"
                : result.Error?.Type == ErrorType.Validation
                    ? "validation_failure"
                    : "failure";

            MessageInstrumentation.TryCompleteActivity(activity, outcome, errorType);
            TryLog(messageKind, messageType, outcome, errorType);

            return result;
        }
        finally
        {
            double elapsedSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            MessageInstrumentation.TryRecordExecution(messageKind, outcome);
            MessageInstrumentation.TryRecordDuration(messageKind, outcome, elapsedSeconds);
            MessageInstrumentation.TryStopActivity(activity);
        }
    }

    private void TryLog(string messageKind, string messageType, string outcome, string? errorType)
    {
        (int Id, string Name, LogLevel Level)? logEvent = outcome switch
        {
            "success" => (1000, "MessageSucceeded", LogLevel.Debug),
            "failure" => (1001, "MessageFailed", LogLevel.Information),
            "validation_failure" => (1002, "MessageValidationFailed", LogLevel.Information),
            "cancelled" => (1003, "MessageCancelled", LogLevel.Debug),
            _ => null
        };

        if (logEvent is null)
        {
            return;
        }

        try
        {
            (int id, string name, LogLevel level) = logEvent.Value;
            EventId eventId = new(id, name);

            if (errorType is null)
            {
                _logger.Log(
                    level,
                    eventId,
                    "SliceForge message {Outcome}: {MessageKind} {MessageType}",
                    outcome,
                    messageKind,
                    messageType);
            }
            else
            {
                _logger.Log(
                    level,
                    eventId,
                    "SliceForge message {Outcome}: {MessageKind} {MessageType} {ErrorType}",
                    outcome,
                    messageKind,
                    messageType,
                    errorType);
            }
        }
        catch (Exception)
        {
            // Instrumentation is best-effort and must not replace message outcomes.
        }
    }
}
