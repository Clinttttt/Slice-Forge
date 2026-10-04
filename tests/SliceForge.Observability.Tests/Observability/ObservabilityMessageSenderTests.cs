using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SliceForge.Messaging;
using SliceForge.Observability.DependencyInjection;
using SliceForge.Observability.Tests.Support;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.DependencyInjection;

namespace SliceForge.Observability.Tests.Observability;

public sealed class ObservabilityMessageSenderTests
{
    [Fact]
    public async Task Non_generic_command_success_preserves_identity_and_emits_child_activity_metrics_and_log()
    {
        ObservabilityProbe probe = new();
        Result expectedResult = Result.Success();
        probe.CommandResult = expectedResult;
        InMemoryLoggerProvider loggerProvider = new();
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<NonGenericCommand, NonGenericCommandHandler>();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        using Activity parent = activityCapture.StartParentActivity();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        NonGenericCommand command = new("payload-must-not-be-recorded");
        using CancellationTokenSource cancellationTokenSource = new();

        Result result = await sender.SendCommandAsync(command, cancellationTokenSource.Token);

        Assert.Same(expectedResult, result);
        Assert.Equal(1, probe.ExecutionCount);
        Assert.Same(command, probe.LastMessage);
        Assert.Equal(cancellationTokenSource.Token, probe.ReceivedToken);

        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal("sliceforge.command", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal(parent.TraceId, activity.TraceId);
        Assert.Equal(parent.SpanId, activity.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
        Assert.Equal("command", activity.GetTagItem("sliceforge.message.kind"));
        Assert.Equal(typeof(NonGenericCommand).FullName, activity.GetTagItem("sliceforge.message.type"));
        Assert.Equal("success", activity.GetTagItem("sliceforge.outcome"));
        Assert.Null(activity.GetTagItem("sliceforge.error.type"));
        Assert.DoesNotContain(command.Payload, string.Join('|', activity.TagObjects.Select(tag => tag.Value)));

        AssertSingleMeasurement(meterCapture, "command", "success");
        CapturedLog log = Assert.Single(loggerProvider.Entries);
        Assert.Equal("SliceForge.Observability", log.Category);
        Assert.Equal(LogLevel.Debug, log.Level);
        Assert.Equal(1000, log.EventId.Id);
        Assert.Equal("command", log.Properties["MessageKind"]);
        Assert.Equal(typeof(NonGenericCommand).FullName, log.Properties["MessageType"]);
        Assert.Equal("success", log.Properties["Outcome"]);
        Assert.DoesNotContain(command.Payload, SerializeLog(log), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Typed_command_success_preserves_result_message_token_and_nullable_value()
    {
        ObservabilityProbe probe = new();
        Result<string?> expectedResult = Result<string?>.Success(null);
        probe.TypedCommandResult = expectedResult;
        ServiceCollection services = CreateServices(probe);
        services.AddSliceForgeCommandHandler<TypedCommand, string?, TypedCommandHandler>();
        services.AddSliceForgeObservability();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        TypedCommand command = new("sensitive-command-value");
        using CancellationTokenSource cancellationTokenSource = new();

        Result<string?> result = await sender.SendCommandAsync<string?>(command, cancellationTokenSource.Token);

        Assert.Same(expectedResult, result);
        Assert.Null(result.Value);
        Assert.Equal(1, probe.ExecutionCount);
        Assert.Same(command, probe.LastMessage);
        Assert.Equal(cancellationTokenSource.Token, probe.ReceivedToken);
    }

    [Fact]
    public async Task Query_success_preserves_typed_result_and_records_query_kind()
    {
        ObservabilityProbe probe = new();
        Result<string> expectedResult = Result<string>.Success("query response");
        probe.QueryResult = expectedResult;
        ServiceCollection services = CreateServices(probe);
        services.AddSliceForgeQueryHandler<SampleQuery, string, SampleQueryHandler>();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        SampleQuery query = new("private query value");

        Result<string> result = await sender.SendQueryAsync(query, CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Equal(1, probe.ExecutionCount);
        Assert.Same(query, probe.LastMessage);

        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal("sliceforge.query", activity.DisplayName);
        Assert.Equal(ActivityKind.Internal, activity.Kind);
        Assert.Equal("query", activity.GetTagItem("sliceforge.message.kind"));
        Assert.Equal(typeof(SampleQuery).FullName, activity.GetTagItem("sliceforge.message.type"));
        AssertSingleMeasurement(meterCapture, "query", "success");
    }

    [Fact]
    public async Task Expected_result_failure_is_returned_and_logged_without_sensitive_error_details()
    {
        ObservabilityProbe probe = new();
        Error error = new("private-error-code", "private error description", ErrorType.Conflict);
        Result expectedResult = Result.Failure(error);
        probe.CommandResult = expectedResult;
        InMemoryLoggerProvider loggerProvider = new();
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<FailureCommand, FailureCommandHandler>();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        FailureCommand command = new("private request value");

        Result result = await sender.SendCommandAsync(command, CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Same(error, result.Error);
        Assert.Equal(1, probe.ExecutionCount);
        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("failure", activity.GetTagItem("sliceforge.outcome"));
        Assert.Equal(nameof(ErrorType.Conflict), activity.GetTagItem("sliceforge.error.type"));
        string activityData = string.Join('|', activity.TagObjects.Select(tag => tag.Value));
        Assert.DoesNotContain(error.Code, activityData, StringComparison.Ordinal);
        Assert.DoesNotContain(error.Description, activityData, StringComparison.Ordinal);
        AssertSingleMeasurement(meterCapture, "command", "failure");

        CapturedLog log = Assert.Single(loggerProvider.Entries);
        Assert.Equal(1001, log.EventId.Id);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal(nameof(ErrorType.Conflict), log.Properties["ErrorType"]);
        string logData = SerializeLog(log);
        Assert.DoesNotContain(command.Payload, logData, StringComparison.Ordinal);
        Assert.DoesNotContain(error.Code, logData, StringComparison.Ordinal);
        Assert.DoesNotContain(error.Description, logData, StringComparison.Ordinal);
        Assert.DoesNotContain("sliceforge.error.code", activity.TagObjects.Select(tag => tag.Key));
    }

    [Fact]
    public async Task Validation_failure_from_inner_decorator_is_observed_without_running_handler()
    {
        ObservabilityProbe probe = new();
        InMemoryLoggerProvider loggerProvider = new();
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<ValidatedCommand, ValidatedCommandHandler>();
        services.AddSliceForgeValidator<ValidatedCommand, RequiredValueValidator>();
        services.AddSliceForgeValidation();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();
        ValidatedCommand command = new("private validation input");

        Result result = await sender.SendCommandAsync(command, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorType.Validation, result.Error?.Type);
        Assert.Equal(0, probe.ExecutionCount);
        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("validation_failure", activity.GetTagItem("sliceforge.outcome"));
        Assert.Equal(nameof(ErrorType.Validation), activity.GetTagItem("sliceforge.error.type"));
        string activityData = string.Join('|', activity.TagObjects.Select(tag => tag.Value));
        Assert.DoesNotContain(command.Value, activityData, StringComparison.Ordinal);
        Assert.DoesNotContain("private validation message", activityData, StringComparison.Ordinal);
        AssertSingleMeasurement(meterCapture, "command", "validation_failure");

        CapturedLog log = Assert.Single(loggerProvider.Entries);
        Assert.Equal(1002, log.EventId.Id);
        Assert.Equal(LogLevel.Information, log.Level);
        Assert.Equal("validation_failure", log.Properties["Outcome"]);
        string logData = string.Join('|', loggerProvider.Entries.Select(SerializeLog));
        Assert.DoesNotContain(command.Value, logData, StringComparison.Ordinal);
        Assert.DoesNotContain("private validation message", logData, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cancellation_is_observed_and_the_original_exception_and_token_are_preserved()
    {
        ObservabilityProbe probe = new();
        using CancellationTokenSource cancellationTokenSource = new();
        OperationCanceledException expectedException = new("private cancellation detail", cancellationTokenSource.Token);
        probe.ExceptionToThrow = expectedException;
        InMemoryLoggerProvider loggerProvider = new();
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<CancellationCommand, CancellationCommandHandler>();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        OperationCanceledException actualException = await Assert.ThrowsAsync<OperationCanceledException>(
            () => sender.SendCommandAsync(new CancellationCommand(), cancellationTokenSource.Token));

        Assert.Same(expectedException, actualException);
        Assert.Equal(cancellationTokenSource.Token, probe.ReceivedToken);
        Assert.Equal(1, probe.ExecutionCount);
        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
        Assert.Equal("cancelled", activity.GetTagItem("sliceforge.outcome"));
        AssertSingleMeasurement(meterCapture, "command", "cancelled");
        CapturedLog log = Assert.Single(loggerProvider.Entries);
        Assert.Equal(1003, log.EventId.Id);
        Assert.Equal("MessageCancelled", log.EventId.Name);
        Assert.Equal(LogLevel.Debug, log.Level);
        Assert.DoesNotContain(expectedException.Message, SerializeLog(log), StringComparison.Ordinal);
        Assert.DoesNotContain(expectedException.Message, string.Join('|', activity.TagObjects.Select(tag => tag.Value)));
    }

    [Fact]
    public async Task Unexpected_exception_is_rethrown_unchanged_without_package_exception_log()
    {
        ObservabilityProbe probe = new();
        InvalidOperationException expectedException = new("private exception detail");
        probe.ExceptionToThrow = expectedException;
        InMemoryLoggerProvider loggerProvider = new();
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<ExceptionCommand, ExceptionCommandHandler>();
        services.AddSliceForgeObservability();

        using ActivityCapture activityCapture = new();
        using MeterCapture meterCapture = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendCommandAsync(new ExceptionCommand(), CancellationToken.None));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, probe.ExecutionCount);
        Activity activity = Assert.Single(activityCapture.Activities);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("exception", activity.GetTagItem("sliceforge.outcome"));
        AssertSingleMeasurement(meterCapture, "command", "exception");
        Assert.Empty(loggerProvider.Entries);
        Assert.DoesNotContain(expectedException.Message, activity.TagObjects.Select(tag => tag.Value?.ToString()));
    }

    [Fact]
    public async Task Telemetry_listener_and_logger_failures_do_not_replace_the_returned_result()
    {
        ObservabilityProbe probe = new();
        Result expectedResult = Result.Success();
        probe.CommandResult = expectedResult;
        InMemoryLoggerProvider loggerProvider = new(throwOnLog: true);
        ServiceCollection services = CreateServices(probe, loggerProvider);
        services.AddSliceForgeCommandHandler<NonGenericCommand, NonGenericCommandHandler>();
        services.AddSliceForgeObservability();

        using FailingTelemetryListeners listeners = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        Result result = await sender.SendCommandAsync(new NonGenericCommand("private"), CancellationToken.None);

        Assert.Same(expectedResult, result);
        Assert.Equal(1, probe.ExecutionCount);
    }

    [Fact]
    public async Task Telemetry_listener_and_logger_failures_do_not_mask_cancellation()
    {
        ObservabilityProbe probe = new();
        OperationCanceledException expectedException = new("cancelled");
        probe.ExceptionToThrow = expectedException;
        ServiceCollection services = CreateServices(probe, new InMemoryLoggerProvider(throwOnLog: true));
        services.AddSliceForgeCommandHandler<CancellationCommand, CancellationCommandHandler>();
        services.AddSliceForgeObservability();

        using FailingTelemetryListeners listeners = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        OperationCanceledException actualException = await Assert.ThrowsAsync<OperationCanceledException>(
            () => sender.SendCommandAsync(new CancellationCommand(), CancellationToken.None));

        Assert.Same(expectedException, actualException);
    }

    [Fact]
    public async Task Telemetry_listener_and_logger_failures_do_not_mask_an_inner_exception()
    {
        ObservabilityProbe probe = new();
        InvalidOperationException expectedException = new("inner failure");
        probe.ExceptionToThrow = expectedException;
        ServiceCollection services = CreateServices(probe, new InMemoryLoggerProvider(throwOnLog: true));
        services.AddSliceForgeCommandHandler<ExceptionCommand, ExceptionCommandHandler>();
        services.AddSliceForgeObservability();

        using FailingTelemetryListeners listeners = new();
        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

        InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendCommandAsync(new ExceptionCommand(), CancellationToken.None));

        Assert.Same(expectedException, actualException);
    }

    private static ServiceCollection CreateServices(
        ObservabilityProbe probe,
        ILoggerProvider? loggerProvider = null)
    {
        ServiceCollection services = new();
        services.AddSliceForgeRuntime();
        services.AddSingleton(probe);

        if (loggerProvider is not null)
        {
            services.AddSingleton<ILoggerFactory>(new InMemoryLoggerFactory(loggerProvider));
        }

        return services;
    }

    private static void AssertSingleMeasurement(MeterCapture capture, string kind, string outcome)
    {
        CapturedMeasurement<long> execution = Assert.Single(capture.Executions);
        Assert.Equal("sliceforge.messaging.executions", execution.InstrumentName);
        Assert.Equal("{execution}", execution.Unit);
        Assert.Equal(1, execution.Value);
        Assert.Equal(kind, execution.Tags["sliceforge.message.kind"]);
        Assert.Equal(outcome, execution.Tags["sliceforge.outcome"]);
        Assert.Equal(
            new[] { "sliceforge.message.kind", "sliceforge.outcome" },
            execution.Tags.Keys.Order(StringComparer.Ordinal));

        CapturedMeasurement<double> duration = Assert.Single(capture.Durations);
        Assert.Equal("sliceforge.messaging.duration", duration.InstrumentName);
        Assert.Equal("s", duration.Unit);
        Assert.True(duration.Value >= 0);
        Assert.Equal(kind, duration.Tags["sliceforge.message.kind"]);
        Assert.Equal(outcome, duration.Tags["sliceforge.outcome"]);
        Assert.Equal(
            new[] { "sliceforge.message.kind", "sliceforge.outcome" },
            duration.Tags.Keys.Order(StringComparer.Ordinal));
    }

    private static string SerializeLog(CapturedLog log) =>
        $"{log.Message}|{string.Join('|', log.Properties.Select(property => $"{property.Key}={property.Value}"))}";
}

internal sealed class RequiredValueValidator : AbstractValidator<ValidatedCommand>
{
    public RequiredValueValidator() =>
        RuleFor(command => command.Value)
            .Equal("accepted")
            .WithMessage("private validation message");
}

internal sealed class FailingTelemetryListeners : IDisposable
{
    private readonly ActivityListener _activityListener = new()
    {
        ShouldListenTo = source => source.Name == SliceForgeInstrumentation.ActivitySourceName,
        Sample = static (ref ActivityCreationOptions<ActivityContext> _) =>
            throw new InvalidOperationException("activity listener failure")
    };

    private readonly MeterListener _meterListener = new();

    public FailingTelemetryListeners()
    {
        ActivitySource.AddActivityListener(_activityListener);
        _meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == SliceForgeInstrumentation.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((_, _, _, _) =>
            throw new InvalidOperationException("counter listener failure"));
        _meterListener.SetMeasurementEventCallback<double>((_, _, _, _) =>
            throw new InvalidOperationException("histogram listener failure"));
        _meterListener.Start();
    }

    public void Dispose()
    {
        _meterListener.Dispose();
        _activityListener.Dispose();
    }
}
