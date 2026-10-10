# ADR 0013: Opt-in Validator Assembly Discovery

## Status

Accepted

## Context

Explicit validator registration is deterministic but repetitive in applications
with many Vertical Slices. Validation already owns an exact-message registration
store that supports multiple validators per message and preserves registration
order.

## Decision

`SliceForge.Validation` provides the opt-in
`AddSliceForgeValidatorsFromAssembly(Assembly)` extension. The caller supplies
one assembly, which is inspected during service registration for concrete,
closed `IValidator<TMessage>` contracts. The discovery result is sorted by
fully qualified validator type name per message and added at the point of the
registration call. Earlier explicit validators run first; later explicit
validators run after discovered validators. Discovered registrations use the
existing Validation registration store, transient concrete-type services, and
the existing immutable route catalog. Duplicate registrations of one validator
type for the same message are rejected before the batch is applied.

Only the supplied assembly is inspected. Abstract, interface, open-generic,
and unrelated types are ignored. Handler and endpoint discovery are separate
concerns; this API does not alter Runtime routing or add request-time
reflection. Explicit validator registration remains supported.

## Consequences

- Applications can reduce repetitive registrations while retaining control
  over assembly boundaries.
- Discovery incurs reflection once during startup; validators are not
  instantiated while scanning and remain resolved from the active DI scope.
- Multiple distinct validators for one message remain supported and execute
  sequentially in deterministic discovery order.
- Reflection/type-loading failures surface as configuration errors with the
  original exception and loader details.
- Assembly discovery is not claimed to be trimming- or Native-AOT-safe.
  Consumers requiring predictable trimming/AOT behavior should use explicit
  validator registration.

This decision partially supersedes ADR 0005's initial choice to keep validator
registration explicit and prohibit scanning. ADR 0005's package boundary,
exact-message validation, sender decoration, error mapping, ordering, and
dependency-lifetime decisions remain in force.
