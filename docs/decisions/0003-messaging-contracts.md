# ADR 0003: SliceForge-Owned Messaging Contracts

## Status

Accepted

## Context

SliceForge needs small command, query, and handler contracts that preserve the
Core result model:

- commands without a value payload return `Task<Result>`;
- commands with a value payload return `Task<Result<TResponse>>`;
- queries return `Task<Result<TResponse>>`.

The contracts must remain usable without ASP.NET Core, dependency injection,
reflection, assembly scanning, or a mediator implementation.

## Decision

SliceForge owns the public messaging contracts under `SliceForge.Messaging`.
The contracts declare only SliceForge interfaces and handler methods. They do
not inherit from MediatR or any other third-party messaging abstraction.

Command response type parameters remain invariant. Handler command and query
type parameters are contravariant because they are consumed as method
parameters. Response type parameters remain invariant because they identify the
exact `Result<TResponse>` contract.

`IBaseCommand` is a common marker for both command shapes. It allows future
command-wide constraints without pretending that every command has the same
response type.

## Consequences

SliceForge can change or add a dispatch mechanism without changing the Core
messaging contracts. Runtime dispatch, dependency injection, handler
resolution, pipeline behaviors, validation, logging, and transaction behavior
remain outside this milestone.

MediatR is not a mandatory Core dependency. MediatR 14.2.0 is technically
compatible with .NET 10, but its current Reciprocal Public License 1.5 and
commercial licensing model require a separate product and licensing decision.
Its direct use would also couple SliceForge's public API to a third-party
contract. MediatR may be evaluated later as an optional adapter outside Core.

## Rejected alternatives

- Inheriting from MediatR request or handler interfaces: rejected because it
  exposes third-party types through SliceForge.Core and creates mandatory
  licensing and coupling concerns.
- Adding a SliceForge dispatcher or sender now: rejected because dispatch is
  outside the approved contract-only scope.
- Adding a non-generic query abstraction: rejected because all queries are
  intentionally modeled as `Result<TResponse>` operations.
