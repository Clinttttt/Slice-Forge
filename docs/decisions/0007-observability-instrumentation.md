# Observability Instrumentation Boundary

Status: Accepted

## Context

Runtime owns the scoped `IMessageSender`; optional Validation decorates it and
must remain inside message-level observability. Core and Runtime should not
acquire logging or telemetry dependencies, and consumers should not need an
OpenTelemetry package just to observe SliceForge message execution.

## Decision

Add optional `SliceForge.Observability`, depending on Runtime,
`Microsoft.Extensions.DependencyInjection.Abstractions`, and
`Microsoft.Extensions.Logging.Abstractions` 10.0.12. It does not depend on
Core directly, Validation, ASP.NET Core, FluentValidation, OpenTelemetry,
MediatR, or Scrutor. Its public surface is only:

```csharp
namespace SliceForge.Observability;

public static class SliceForgeInstrumentation
{
    public const string ActivitySourceName = "SliceForge.Observability";
    public const string MeterName = "SliceForge.Observability";
    public const string LoggerCategoryName = "SliceForge.Observability";
}

namespace SliceForge.Observability.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSliceForgeObservability(
        this IServiceCollection services);
}
```

`AddSliceForgeObservability` wraps the one supported scoped, unkeyed
`IMessageSender`. It converts the captured implementation type or factory into
a private keyed sender descriptor and registers an outer scoped decorator that
resolves that key. This lets Microsoft DI retain ownership and scoped/disposal
tracking. It does not build a provider during registration or recursively
resolve the unkeyed sender. Missing, ambiguous, keyed, instance, and
unsupported-lifetime registrations and repeated enablement are rejected.

Register Runtime, then handlers and validators, then Validation if used, and
Observability last:

```text
ObservabilityMessageSender
    ↓
ValidationMessageSender (optional)
    ↓
DefaultMessageSender
    ↓
Handler
```

The decorator invokes its inner sender exactly once and returns the same
`Result`/`Result<T>` object. It preserves the message instance and
`CancellationToken`. Validation failures are observed as returned Results
with `ErrorType.Validation`. SliceForge does not add a public pipeline seam.

One process-wide `ActivitySource` and `Meter` use the published instrumentation
names. Command and query activities are `sliceforge.command` and
`sliceforge.query`, both `ActivityKind.Internal`, and inherit normal
`Activity.Current` parentage. Activities carry only message kind, exact runtime
message type, outcome, and returned failure `ErrorType`. Outcomes are
`success`, `failure`, `validation_failure`, `cancelled`, and `exception`.
Success status is `Ok`; expected failures, validation failures, and
cancellation are `Unset`; unexpected exceptions are `Error`.

The only metrics are `sliceforge.messaging.executions`
(`Counter<long>`, unit `{execution}`) and `sliceforge.messaging.duration`
(`Histogram<double>`, unit `s`). Both carry only message kind and outcome
dimensions. Message type, error type, error code, and other
application-defined values are excluded from metric dimensions.

Structured log events use category `SliceForge.Observability`:

| Event ID | Name | Level |
| --- | --- | --- |
| 1000 | `MessageSucceeded` | Debug |
| 1001 | `MessageFailed` | Information |
| 1002 | `MessageValidationFailed` | Information |
| 1003 | `MessageCancelled` | Debug |

Log fields are message kind, message type, outcome, and returned failure error
type only. Payloads, request/validation values or messages, error descriptions
or codes, credentials, and exception details are never emitted. Unexpected
exceptions are marked in activity/metrics and rethrown unchanged, without an
Observability exception log; the outer exception owner logs once. An
`OperationCanceledException` remains cancellation and is rethrown unchanged.
Activity, metric, and log emission is best-effort and cannot replace a Result
or mask cancellation/application exceptions. If no `ILoggerFactory` exists,
the package uses a null logger and still emits activities and metrics.

OpenTelemetry is an optional consumer integration. Consumers subscribe to the
published ActivitySource and Meter names; they own `service.name`, resource
configuration, exporters, sampling, and the telemetry backend. SliceForge owns
only instrumentation identity and has no observability options until a real
consumer-controlled setting is demonstrated.

## Consequences

Runtime and Core stay independent of observability. Small DI descriptor
replacement logic remains package-local instead of becoming a public generic
decorator abstraction. Consumers must register Observability after Validation
so validation failures reach the outer decorator. Instrumentation listener
tests are serialized because ActivitySource and Meter are process-wide.
