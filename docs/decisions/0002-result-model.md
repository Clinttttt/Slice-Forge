# Result Model

Status: Accepted

## Context

SliceForge needs a small, framework-independent result model for expected application outcomes. The model must support both value-less results and typed results while preserving the future pipeline requirement that `Result<T>` can be handled as a `Result`.

## Decision

- `ErrorType` contains generic application outcome categories and no HTTP status codes.
- `Error` is an immutable sealed class with stable code, user-safe description, category, and defensively copied validation details.
- `Result` is a non-sealed class with a `private protected` constructor so only SliceForge types in the same assembly can derive from it.
- `Result<T>` is sealed and derives from `Result`.
- Successful results expose `Error == null`; failed results expose a non-null error.
- `Result<T>.Value` throws `InvalidOperationException` when accessed after failure.
- Nullable success values are determined by `T` and nullable annotations; the result model does not impose a blanket runtime null check.
- Results and errors do not override equality or hash-code behavior in Milestone 1.
- Success and failure creation uses explicit factories. No implicit conversions or convenience operations are included.
- `IValidationResult<T>` is deferred until a validation pipeline requires a proven public extension point.

## Consequences

Core remains independent of ASP.NET Core, MediatR, FluentValidation, dependency injection, logging, reflection, and HTTP concepts. The public API stays small while preserving typed-result inheritance for future pipeline composition. Equality semantics can be introduced later if a demonstrated consumer requirement justifies them.
