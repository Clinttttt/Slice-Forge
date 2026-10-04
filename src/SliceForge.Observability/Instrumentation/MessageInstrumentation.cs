using System.Diagnostics;
using System.Diagnostics.Metrics;
using SliceForge.Observability;

namespace SliceForge.Observability.Instrumentation;

internal static class MessageInstrumentation
{
    private static readonly ActivitySource ActivitySource =
        new(SliceForgeInstrumentation.ActivitySourceName);

    private static readonly Meter Meter = new(SliceForgeInstrumentation.MeterName);

    private static readonly Counter<long> Executions =
        Meter.CreateCounter<long>("sliceforge.messaging.executions", "{execution}");

    private static readonly Histogram<double> Duration =
        Meter.CreateHistogram<double>("sliceforge.messaging.duration", "s");

    public static Activity? TryStartActivity(string activityName, string messageKind, string messageType)
    {
        Activity? activity = null;

        try
        {
            activity = ActivitySource.StartActivity(activityName, ActivityKind.Internal);
            activity?.SetTag("sliceforge.message.kind", messageKind);
            activity?.SetTag("sliceforge.message.type", messageType);
            return activity;
        }
        catch (Exception)
        {
            TryStopActivity(activity);
            return null;
        }
    }

    public static void TryCompleteActivity(Activity? activity, string outcome, string? errorType)
    {
        if (activity is null)
        {
            return;
        }

        try
        {
            activity.SetTag("sliceforge.outcome", outcome);

            if (errorType is not null)
            {
                activity.SetTag("sliceforge.error.type", errorType);
            }

            if (outcome == "success")
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
            else if (outcome == "exception")
            {
                activity.SetStatus(ActivityStatusCode.Error);
            }
        }
        catch (Exception)
        {
            // Listener callbacks are external instrumentation and must not affect the send.
        }
    }

    public static void TryRecordExecution(string messageKind, string outcome)
    {
        try
        {
            TagList tags = new()
            {
                { "sliceforge.message.kind", messageKind },
                { "sliceforge.outcome", outcome }
            };

            Executions.Add(1, tags);
        }
        catch (Exception)
        {
            // Meter listeners are external instrumentation and must not affect the send.
        }
    }

    public static void TryRecordDuration(string messageKind, string outcome, double elapsedSeconds)
    {
        try
        {
            TagList tags = new()
            {
                { "sliceforge.message.kind", messageKind },
                { "sliceforge.outcome", outcome }
            };

            Duration.Record(elapsedSeconds, tags);
        }
        catch (Exception)
        {
            // Meter listeners are external instrumentation and must not affect the send.
        }
    }

    public static void TryStopActivity(Activity? activity)
    {
        try
        {
            activity?.Dispose();
        }
        catch (Exception)
        {
            // Listener callbacks are external instrumentation and must not affect the send.
        }
    }
}
