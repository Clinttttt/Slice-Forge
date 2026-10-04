using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SliceForge.AspNetCore.Results;
using SliceForge.Messaging;
using SliceForge.Observability;
using SliceForge.Observability.DependencyInjection;
using SliceForge.Results;
using SliceForge.Runtime.DependencyInjection;
using SliceForge.Runtime.Messaging;
using SliceForge.Validation.DependencyInjection;

ConcurrentBag<string> activityOutcomes = [];
ConcurrentBag<string> metricOutcomes = [];

using ActivityListener activityListener = new()
{
    ShouldListenTo = source => source.Name == SliceForgeInstrumentation.ActivitySourceName,
    Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
        ActivitySamplingResult.AllDataAndRecorded,
    ActivityStopped = activity =>
    {
        if (activity.GetTagItem("sliceforge.outcome") is string outcome)
        {
            activityOutcomes.Add(outcome);
        }
    },
};
ActivitySource.AddActivityListener(activityListener);

using MeterListener meterListener = new();
meterListener.InstrumentPublished = (instrument, listener) =>
{
    if (instrument.Meter.Name == SliceForgeInstrumentation.MeterName)
    {
        listener.EnableMeasurementEvents(instrument);
    }
};
meterListener.SetMeasurementEventCallback<long>(
    (instrument, _, tags, _) =>
    {
        if (instrument.Name != "sliceforge.messaging.executions")
        {
            return;
        }

        foreach (KeyValuePair<string, object?> tag in tags)
        {
            if (tag.Key == "sliceforge.outcome" && tag.Value is string outcome)
            {
                metricOutcomes.Add(outcome);
            }
        }
    });
meterListener.Start();

ServiceCollection services = new();
services.AddSliceForgeRuntime();
services.AddSliceForgeCommandHandler<ValidatedCommand, Guid, ValidatedCommandHandler>();
services.AddSliceForgeCommandHandler<MarkerCommand, MarkerCommandHandler>();
services.AddSliceForgeQueryHandler<EchoQuery, string, EchoQueryHandler>();
services.AddSliceForgeValidator<ValidatedCommand, ValidatedCommandValidator>();
services.AddSliceForgeValidation();
services.AddSliceForgeObservability();

using ServiceProvider provider = services.BuildServiceProvider();
using IServiceScope scope = provider.CreateScope();
IMessageSender sender = scope.ServiceProvider.GetRequiredService<IMessageSender>();

Result<Guid> validCommandResult = await sender.SendCommandAsync<Guid>(
    new ValidatedCommand("packaged consumer"),
    CancellationToken.None);
Require(validCommandResult.IsSuccess, "A valid typed command should succeed.");
Require(ConsumerCounters.ValidatedCommandCalls == 1, "The valid command handler should run once.");

IResult mappedHttpResult = validCommandResult.ToHttpResult(
    id => Results.Created($"/package-smoke/{id}", new { id }));
Require(mappedHttpResult is not null, "AspNetCore's IResult API should be available to this SDK consumer.");

Result<Guid> invalidCommandResult = await sender.SendCommandAsync<Guid>(
    new ValidatedCommand(string.Empty),
    CancellationToken.None);
Require(
    !invalidCommandResult.IsSuccess && invalidCommandResult.Error?.Type == ErrorType.Validation,
    "The packaged Validation layer should return a validation failure.");
Require(
    ConsumerCounters.ValidatedCommandCalls == 1,
    "A validation failure must not reach the handler.");

Result markerResult = await sender.SendCommandAsync(new MarkerCommand(), CancellationToken.None);
Require(markerResult.IsSuccess && ConsumerCounters.MarkerCommandCalls == 1, "The non-generic command should dispatch.");

Result<string> queryResult = await sender.SendQueryAsync<string>(
    new EchoQuery("query contract"),
    CancellationToken.None);
Require(queryResult.IsSuccess && queryResult.Value == "query contract", "The query should dispatch and return its value.");

Result<string?> nullableSuccess = Result<string?>.Success(null);
Require(nullableSuccess.IsSuccess && nullableSuccess.Value is null, "Core nullable result contracts should compile and work.");

Require(
    activityOutcomes.Contains("validation_failure"),
    "The outer Observability package should record the inner validation failure.");
Require(
    metricOutcomes.Contains("validation_failure"),
    "The outer Observability package should emit a validation-failure metric outcome.");

Console.WriteLine("Package-only consumer smoke validation passed.");
return 0;

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

internal static class ConsumerCounters
{
    public static int ValidatedCommandCalls;

    public static int MarkerCommandCalls;
}

internal sealed record ValidatedCommand(string Name) : ICommand<Guid>;

internal sealed class ValidatedCommandValidator : AbstractValidator<ValidatedCommand>
{
    public ValidatedCommandValidator()
    {
        RuleFor(command => command.Name).NotEmpty();
    }
}

internal sealed class ValidatedCommandHandler : ICommandHandler<ValidatedCommand, Guid>
{
    public Task<Result<Guid>> HandleAsync(ValidatedCommand command, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumerCounters.ValidatedCommandCalls);
        return Task.FromResult(Result<Guid>.Success(Guid.NewGuid()));
    }
}

internal sealed record MarkerCommand : ICommand;

internal sealed class MarkerCommandHandler : ICommandHandler<MarkerCommand>
{
    public Task<Result> HandleAsync(MarkerCommand command, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref ConsumerCounters.MarkerCommandCalls);
        return Task.FromResult(Result.Success());
    }
}

internal sealed record EchoQuery(string Text) : IQuery<string>;

internal sealed class EchoQueryHandler : IQueryHandler<EchoQuery, string>
{
    public Task<Result<string>> HandleAsync(EchoQuery query, CancellationToken cancellationToken)
    {
        return Task.FromResult(Result<string>.Success(query.Text));
    }
}
