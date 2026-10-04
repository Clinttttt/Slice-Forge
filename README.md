# SliceForge

SliceForge is an early-stage toolkit for building composable .NET applications with Vertical Slice Architecture. The repository has completed the repository foundation, Core result model, messaging contracts, Runtime execution, optional Validation, ASP.NET Core Result mapping, the end-to-end Sample API, and optional Logging & Observability.

The current package boundaries are intentionally explicit:

- `SliceForge.Core` contains framework-independent building blocks.
- `SliceForge.Runtime` contains explicit message routing and dependency-injection integration over Core.
- `SliceForge.Validation` optionally decorates Runtime message sending with explicit FluentValidation routes.
- `SliceForge.Observability` optionally decorates message sending with structured logs, activities, and metrics.
- `SliceForge.AspNetCore` maps Core Results to ASP.NET Core HTTP results and references only `SliceForge.Core` plus the `Microsoft.AspNetCore.App` shared framework.

Runtime owns no assembly scanning, polymorphic routing, validation, logging, or mediator dependency. Observability uses built-in .NET instrumentation and Logging abstractions; OpenTelemetry exporters and resource identity are optional consumer configuration. Consumer applications own their business rules, persistence, authentication, and provider integrations.

ASP.NET endpoints are mapped explicitly by consumer applications. `ToHttpResult`
maps expected failures while success callbacks preserve consumer-selected
responses. Consumers configure `AddProblemDetails()` and
`UseExceptionHandler()` directly; HTTP 500 is reserved for unexpected
exceptions. SliceForge does not discover endpoints or choose authentication
schemes.

`SliceForge.Observability` emits command/query activities, low-cardinality
execution metrics, and structured message outcome logs. It does not depend on
OpenTelemetry or own the consumer's `service.name`, exporters, or sampling.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
