# SliceForge Architecture

SliceForge owns reusable application plumbing; consumer applications own business decisions. The repository uses separate packages to keep dependency boundaries explicit.

```text
Consumer application
        |
        +--> SliceForge.AspNetCore
        |          |
        |          +--> SliceForge.Core
        |
        +--> SliceForge.Runtime
        |          |
        |          +--> SliceForge.Core
        |
        +--> SliceForge.Core
```

`SliceForge.Core` must remain independent of ASP.NET Core and dependency injection. It must not reference HTTP status codes, `HttpContext`, minimal API result types, ASP.NET middleware, or mediator libraries. ASP.NET-specific endpoint, error, and HTTP mapping capabilities belong in `SliceForge.AspNetCore`.
