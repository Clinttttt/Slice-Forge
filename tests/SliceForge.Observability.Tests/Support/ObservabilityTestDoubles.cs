using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using SliceForge.Messaging;
using SliceForge.Observability;
using SliceForge.Results;
using SliceForge.Runtime.Messaging;

namespace SliceForge.Observability.Tests.Support;

internal sealed class ObservabilityProbe
{
    public int ExecutionCount { get; set; }

    public object? LastMessage { get; set; }

    public CancellationToken ReceivedToken { get; set; }

    public Result CommandResult { get; set; } = Result.Success();

    public Result<string?> TypedCommandResult { get; set; } = Result<string?>.Success("typed response");

    public Result<string> QueryResult { get; set; } = Result<string>.Success("query response");

    public Exception? ExceptionToThrow { get; set; }
}

internal sealed class InMemoryLoggerProvider(bool throwOnLog = false) : ILoggerProvider
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyCollection<CapturedLog> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new InMemoryLogger(categoryName, _entries, throwOnLog);

    public void Dispose()
    {
    }

    private sealed class InMemoryLogger(
        string categoryName,
        ConcurrentQueue<CapturedLog> entries,
        bool throwOnLog) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (throwOnLog)
            {
                throw new InvalidOperationException("test logger failure");
            }

            Dictionary<string, object?> properties = state is IEnumerable<KeyValuePair<string, object?>> values
                ? values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);

            entries.Enqueue(new CapturedLog(
                categoryName,
                logLevel,
                eventId,
                properties,
                formatter(state, exception)));
        }
    }
}

internal sealed record CapturedLog(
    string Category,
    LogLevel Level,
    EventId EventId,
    IReadOnlyDictionary<string, object?> Properties,
    string Message);

internal sealed class InMemoryLoggerFactory(ILoggerProvider provider) : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName) => provider.CreateLogger(categoryName);

    public void AddProvider(ILoggerProvider additionalProvider) =>
        throw new NotSupportedException("The test logger factory has a fixed provider.");

    public void Dispose() => provider.Dispose();
}

internal sealed class ActivityCapture : IDisposable
{
    private readonly ActivityListener _sliceForgeListener;
    private readonly List<Activity> _activities = [];
    private ActivityListener? _parentListener;
    private ActivitySource? _parentSource;

    public ActivityCapture()
    {
        _sliceForgeListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == SliceForgeInstrumentation.ActivitySourceName,
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => _activities.Add(activity)
        };

        ActivitySource.AddActivityListener(_sliceForgeListener);
    }

    public IReadOnlyList<Activity> Activities => _activities;

    public Activity StartParentActivity()
    {
        _parentListener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "SliceForge.Observability.Tests.Parent",
            Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };

        ActivitySource.AddActivityListener(_parentListener);
        _parentSource = new ActivitySource("SliceForge.Observability.Tests.Parent");

        return _parentSource.StartActivity("test.parent", ActivityKind.Server)
            ?? throw new InvalidOperationException("The test parent activity was not created.");
    }

    public void Dispose()
    {
        _parentSource?.Dispose();
        _parentListener?.Dispose();
        _sliceForgeListener.Dispose();
    }
}

internal sealed record CapturedMeasurement<T>(
    string InstrumentName,
    string? Unit,
    T Value,
    IReadOnlyDictionary<string, object?> Tags);

internal sealed class MeterCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly List<CapturedMeasurement<long>> _executions = [];
    private readonly List<CapturedMeasurement<double>> _durations = [];

    public MeterCapture()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SliceForgeInstrumentation.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
            _executions.Add(new CapturedMeasurement<long>(instrument.Name, instrument.Unit, value, CopyTags(tags))));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            _durations.Add(new CapturedMeasurement<double>(instrument.Name, instrument.Unit, value, CopyTags(tags))));
        _listener.Start();
    }

    public IReadOnlyList<CapturedMeasurement<long>> Executions => _executions;

    public IReadOnlyList<CapturedMeasurement<double>> Durations => _durations;

    public void Dispose() => _listener.Dispose();

    private static IReadOnlyDictionary<string, object?> CopyTags(
        ReadOnlySpan<KeyValuePair<string, object?>> tags) =>
        tags.ToArray().ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
}

internal sealed record NonGenericCommand(string Payload) : ICommand;

internal sealed record TypedCommand(string Payload) : ICommand<string?>;

internal sealed record SampleQuery(string Payload) : IQuery<string>;

internal sealed record FailureCommand(string Payload) : ICommand;

internal sealed record ValidatedCommand(string Value) : ICommand;

internal sealed record CancellationCommand : ICommand;

internal sealed record ExceptionCommand : ICommand;

internal sealed class NonGenericCommandHandler(ObservabilityProbe probe) : ICommandHandler<NonGenericCommand>
{
    public Task<Result> HandleAsync(NonGenericCommand command, CancellationToken cancellationToken)
    {
        Record(probe, command, cancellationToken);
        return Task.FromResult(probe.CommandResult);
    }

    internal static void Record(ObservabilityProbe probe, object message, CancellationToken cancellationToken)
    {
        probe.ExecutionCount++;
        probe.LastMessage = message;
        probe.ReceivedToken = cancellationToken;
    }
}

internal sealed class TypedCommandHandler(ObservabilityProbe probe) : ICommandHandler<TypedCommand, string?>
{
    public Task<Result<string?>> HandleAsync(TypedCommand command, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, command, cancellationToken);
        return Task.FromResult(probe.TypedCommandResult);
    }
}

internal sealed class SampleQueryHandler(ObservabilityProbe probe) : IQueryHandler<SampleQuery, string>
{
    public Task<Result<string>> HandleAsync(SampleQuery query, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, query, cancellationToken);
        return Task.FromResult(probe.QueryResult);
    }
}

internal sealed class FailureCommandHandler(ObservabilityProbe probe) : ICommandHandler<FailureCommand>
{
    public Task<Result> HandleAsync(FailureCommand command, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, command, cancellationToken);
        return Task.FromResult(probe.CommandResult);
    }
}

internal sealed class ValidatedCommandHandler(ObservabilityProbe probe) : ICommandHandler<ValidatedCommand>
{
    public Task<Result> HandleAsync(ValidatedCommand command, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, command, cancellationToken);
        return Task.FromResult(probe.CommandResult);
    }
}

internal sealed class CancellationCommandHandler(ObservabilityProbe probe) : ICommandHandler<CancellationCommand>
{
    public Task<Result> HandleAsync(CancellationCommand command, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, command, cancellationToken);
        throw probe.ExceptionToThrow ?? new OperationCanceledException(cancellationToken);
    }
}

internal sealed class ExceptionCommandHandler(ObservabilityProbe probe) : ICommandHandler<ExceptionCommand>
{
    public Task<Result> HandleAsync(ExceptionCommand command, CancellationToken cancellationToken)
    {
        NonGenericCommandHandler.Record(probe, command, cancellationToken);
        throw probe.ExceptionToThrow ?? new InvalidOperationException("unexpected handler failure");
    }
}
