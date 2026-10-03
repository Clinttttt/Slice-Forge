# Validation Decoration Boundary

Status: Accepted

## Context

SliceForge.Runtime owns message execution, exact concrete-message routing, and
handler resolution. SliceForge.Core owns only framework-independent Results and
messaging contracts. Validation must be optional and must not introduce
FluentValidation or a MediatR-style public pipeline into either package.

## Decision

Add validation as the separate `SliceForge.Validation` package. It decorates
the scoped `IMessageSender` registered by Runtime:

```text
ValidationMessageSender
        ↓
DefaultMessageSender
        ↓
Handler
```

The package exposes only these registration methods:

```csharp
AddSliceForgeValidation(IServiceCollection services)

AddSliceForgeValidator<TMessage, TValidator>(IServiceCollection services)
    where TValidator : class, IValidator<TMessage>
```

Validator registration is explicit and keyed by the exact concrete message
type. Assembly scanning, base-type discovery, interface discovery, and
validators registered outside the SliceForge registration method are not used.

Validation route metadata is immutable and cached as a singleton. Validator
instances are resolved from the current DI scope at execution time and remain
transient by default. Multiple validators run sequentially in registration
order.

The internal route produces an `Error?`, not a `Result`. The sender decorator
constructs the result appropriate to the sender overload:

```text
ICommand          → Result.Failure(error)
ICommand<T>       → Result<T>.Failure(error)
IQuery<T>         → Result<T>.Failure(error)
```

Validation failures use:

```text
ErrorType.Validation
Code: Validation.Failed
Description: One or more validation errors occurred.
```

Property names and messages are grouped in first-seen order. Empty property
names are preserved for object-level failures. Duplicate property/message
pairs are removed. Validation exceptions and cancellation propagate as
exceptions and are never converted into `Result.Failure`.

`AddSliceForgeValidation` requires exactly one existing scoped
`IMessageSender`, rejects duplicate enablement, and replaces the captured
registration with a scoped decorator factory. The captured sender is activated
directly from its original service descriptor; the decorator does not resolve
`IMessageSender` recursively and does not require Scrutor.

## Consequences

FluentValidation remains optional: consumers that do not reference
`SliceForge.Validation` do not take a validation dependency. Core remains
framework-independent, and Runtime remains free of FluentValidation.

Future observability can use the same targeted sender-decoration mechanism so
registration order produces:

```text
ObservabilityMessageSender
        ↓
ValidationMessageSender
        ↓
DefaultMessageSender
```

No public execution-stage abstraction is introduced at this milestone. This
avoids recreating `IPipelineBehavior` under a different name.
