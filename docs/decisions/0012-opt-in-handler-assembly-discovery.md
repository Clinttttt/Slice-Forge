# ADR 0012: Opt-in Handler Assembly Discovery

## Status

Accepted

## Context

Explicit handler registration makes composition visible and deterministic, but
can become repetitive as a Vertical Slice application accumulates handlers.
Runtime already has one exact route per concrete message and a shared
registration store used by manual registration and dispatch.

## Decision

Runtime offers the opt-in extension
`AddSliceForgeHandlersFromAssembly(Assembly)`. The application supplies one
assembly explicitly. At registration time, Runtime inspects only that
assembly's concrete, closed classes for the supported
`ICommandHandler<TCommand>`, `ICommandHandler<TCommand, TResponse>`, and
`IQueryHandler<TQuery, TResponse>` contracts. Discovered handlers are
registered as transient DI services and their route metadata is added to the
existing route-registration store and catalog.

Manual registration remains supported. Both paths use the same route store,
so duplicate routes—including a command/query collision for one concrete
message type, repeated assembly registration, and manual/discovered
conflicts—fail during service registration. Discovery order is stable, and
conflicts found within the supplied assembly are reported before its routes
are added. Assemblies with no supported handlers are a no-op.

Discovery is startup-only. Dispatch continues to use the existing catalog and
exact `message.GetType()` route lookup. Runtime does not scan loaded or
referenced assemblies, discover validators or endpoints, or perform
request-time reflection. Endpoint mapping remains application-owned and
explicit.

## Consequences

- Applications can reduce repetitive handler registrations while retaining
  explicit control over assembly boundaries.
- Reflection has a one-time registration cost; handler activation and
  constructor injection remain owned by normal DI at execution time.
- Exact routing, transient handler lifetime, scoped sender lifetime,
  cancellation propagation, and Result semantics are unchanged.
- Reflection type-loading failures surface as configuration errors with the
  original exception and loader details; conflicts identify messages and
  discovered handler types.
- The scanner is not claimed to be trimming- or Native-AOT-safe. Applications
  requiring verified trimming/AOT compatibility should use explicit
  registrations until support is established.

This ADR supersedes ADR 0004 only on its statement that Runtime never performs
handler assembly scanning. ADR 0004's Core-isolation and Runtime execution
boundary decisions remain accepted. ADR 0003 continues to describe Core
contracts, which remain independent of reflection and discovery.

## Rejected alternatives

- Scanning all loaded or referenced assemblies: rejected because it hides the
  application boundary and makes registration dependent on process contents.
- Request-time discovery or polymorphic/base-type route matching: rejected
  because it changes dispatch behavior and duplicates the route catalog.
- Validator or endpoint discovery: deferred to separate decisions; this API
  is limited to handler contracts.
- Replacing explicit registration: rejected; explicit registration remains a
  supported option, including for trimming/AOT-sensitive applications.
