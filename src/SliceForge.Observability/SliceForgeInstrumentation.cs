namespace SliceForge.Observability;

/// <summary>
/// Provides stable names for subscribing to SliceForge instrumentation.
/// </summary>
public static class SliceForgeInstrumentation
{
    /// <summary>The activity source name used for message execution activities.</summary>
    public const string ActivitySourceName = "SliceForge.Observability";

    /// <summary>The meter name used for message execution metrics.</summary>
    public const string MeterName = "SliceForge.Observability";

    /// <summary>The logger category name used for message execution events.</summary>
    public const string LoggerCategoryName = "SliceForge.Observability";
}
