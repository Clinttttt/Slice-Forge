# SliceForge

SliceForge is an early-stage toolkit for building composable .NET applications with Vertical Slice Architecture. The repository has completed the repository foundation, Core result model, messaging contracts, Runtime execution, optional Validation, and ASP.NET Core Result-mapping milestones. The Sample API is next.

The current package boundaries are intentionally explicit:

- `SliceForge.Core` contains framework-independent building blocks.
- `SliceForge.Runtime` contains explicit message routing and dependency-injection integration over Core.
- `SliceForge.Validation` optionally decorates Runtime message sending with explicit FluentValidation routes.
- `SliceForge.AspNetCore` maps Core Results to ASP.NET Core HTTP results and references only `SliceForge.Core` plus the `Microsoft.AspNetCore.App` shared framework.

Runtime owns no assembly scanning, polymorphic routing, validation, logging, or mediator dependency. Consumer applications will own their business rules, persistence, authentication, and provider integrations.

ASP.NET endpoints are mapped explicitly by consumer applications. `ToHttpResult`
maps expected failures while success callbacks preserve consumer-selected
responses. Consumers configure `AddProblemDetails()` and
`UseExceptionHandler()` directly; HTTP 500 is reserved for unexpected
exceptions. SliceForge does not discover endpoints or choose authentication
schemes.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
