# SliceForge

SliceForge is an early-stage toolkit for building composable .NET applications with Vertical Slice Architecture. The repository is currently at Milestone 0: repository foundation only.

The initial package boundary is intentionally small:

- `SliceForge.Core` contains framework-independent building blocks.
- `SliceForge.AspNetCore` contains ASP.NET Core integration and may reference `SliceForge.Core`.

No SliceForge framework behavior has been introduced yet. Consumer applications will own their business rules, persistence, authentication, and provider integrations.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
