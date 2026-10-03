# SliceForge

SliceForge is an early-stage toolkit for building composable .NET applications with Vertical Slice Architecture. The repository has completed the repository foundation, Core result model, messaging contracts, and Runtime execution milestones. Validation remains the next milestone.

The current package boundaries are intentionally explicit:

- `SliceForge.Core` contains framework-independent building blocks.
- `SliceForge.Runtime` contains explicit message routing and dependency-injection integration over Core.
- `SliceForge.AspNetCore` contains ASP.NET Core integration and may reference `SliceForge.Core`.

Runtime owns no assembly scanning, polymorphic routing, validation, logging, or mediator dependency. Consumer applications will own their business rules, persistence, authentication, and provider integrations.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
