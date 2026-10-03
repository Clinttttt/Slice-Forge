# Runtime Execution Boundary

Status: Accepted

## Context

Milestone 2 established SliceForge-owned command, query, and handler contracts
in `SliceForge.Core`, but deliberately did not define execution, handler
resolution, or dependency-injection integration. Validation and observability
will eventually need a stable execution boundary without coupling Core to
MediatR or ASP.NET Core.

## Decision

Add `SliceForge.Runtime` as a separate package that references
`SliceForge.Core`. Runtime owns the public `IMessageSender` abstraction, the
internal default sender, immutable route metadata, and explicit registration
methods for concrete command and query handlers.

Routes are registered explicitly and keyed by the exact concrete message type.
Runtime does not scan assemblies or discover handlers through base types or
interfaces. A missing route, missing handler, or multiple handlers is a
technical configuration failure and throws `InvalidOperationException`.

`IMessageSender` and its default implementation are scoped. Handlers are
transient by default. Route metadata is immutable and singleton-scoped.

Runtime uses `IServiceProvider` only at its internal dynamic handler-resolution
boundary, where a closed generic route resolves its exact handler service. The
provider is not part of public APIs, is not passed to handlers, does not escape
Runtime, and is not used as a general service locator.

Runtime does not add validation stages, logging stages, decorators, a public
pipeline abstraction, FluentValidation, MediatR, assembly scanning, or nested
service scopes. Future validation and observability packages may decorate
`IMessageSender` without changing Core contracts.

## Relationship to ADR 0001

This ADR supersedes ADR 0001 only regarding its original assumption that the
repository should begin with two projects. ADR 0001's Core-isolation decision
remains accepted: Core stays framework-independent and must not depend on
ASP.NET Core or dependency-injection infrastructure.

## Consequences

Runtime requires `Microsoft.Extensions.DependencyInjection.Abstractions`,
centrally versioned, while Core remains free of DI dependencies. The Runtime
package can be used by non-ASP.NET consumers, and `SliceForge.AspNetCore` does
not reference Runtime until an ASP.NET feature actually consumes
`IMessageSender`.

The explicit registration model makes missing and duplicate routes visible and
deterministic. Polymorphic routing, validation composition, logging
composition, and optional MediatR adapters remain separate future decisions.
