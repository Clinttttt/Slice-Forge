# Initial Package Boundaries

Status: Accepted

## Context

SliceForge needs reusable application plumbing without coupling framework-independent code to ASP.NET Core. The first milestone should establish only the package boundaries justified by the current product contract.

## Decision

Start with two projects:

- `SliceForge.Core` contains framework-independent contracts and primitives.
- `SliceForge.AspNetCore` contains ASP.NET Core integration and references `SliceForge.Core`.

Consumer applications may reference either package, but `SliceForge.Core` must not reference ASP.NET Core.

## Consequences

HTTP-specific concerns remain outside Core, keeping the core package reusable in non-HTTP consumers. Additional packages will be introduced only when a real dependency boundary requires them.
