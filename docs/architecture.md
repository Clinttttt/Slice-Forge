# SliceForge Architecture

SliceForge owns reusable application plumbing; consumer applications own business decisions. The initial repository establishes two packages to keep dependency boundaries explicit.

```text
Consumer application
        |
        +--> SliceForge.AspNetCore
        |          |
        |          +--> SliceForge.Core
        |
        +--> SliceForge.Core
```

`SliceForge.Core` must remain independent of ASP.NET Core. It must not reference HTTP status codes, `HttpContext`, minimal API result types, or ASP.NET middleware. ASP.NET-specific endpoint, error, and HTTP mapping capabilities belong in `SliceForge.AspNetCore`.

Milestone 0 contains only project scaffolding and repository-wide build settings. Result primitives, messaging, validation, endpoint discovery, and other framework capabilities are intentionally deferred to later milestones.
