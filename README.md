# SliceForge

SliceForge is a composable Vertical Slice toolkit for modern .NET applications.
It provides Result-based application flows, explicit command/query dispatch,
validation, ASP.NET Core integration, observability, project templates, and CLI
tooling without requiring MediatR. The `0.1.0-preview.3` packages target .NET
10 as a coordinated preview set; APIs and package boundaries may still evolve
before a stable release.

## Packages

| Package | Responsibility |
|---|---|
| `SliceForge.Core` | Framework-independent results, errors, and messaging contracts. |
| `SliceForge.Runtime` | Explicit command/query routing and handler registration. |
| `SliceForge.Validation` | Optional FluentValidation sender decorator. |
| `SliceForge.AspNetCore` | Expected Result-to-HTTP mapping; success responses remain application-owned. |
| `SliceForge.Observability` | Optional .NET logging, activity, and metrics instrumentation. |
| `SliceForge.Templates` | A `dotnet new` template for a .NET 10 Minimal API consumer. |
| `SliceForge.Tool` | A .NET global tool for creating applications from the template and checking the local environment. |

Install only the capabilities your application uses. For example:

```powershell
dotnet add package SliceForge.Core --version 0.1.0-preview.3
dotnet add package SliceForge.Runtime --version 0.1.0-preview.3
dotnet add package SliceForge.Validation --version 0.1.0-preview.3
dotnet add package SliceForge.AspNetCore --version 0.1.0-preview.3
dotnet add package SliceForge.Observability --version 0.1.0-preview.3
```

Core is a transitive dependency of Runtime, Validation, and AspNetCore. A direct
Core reference is also appropriate when application code directly consumes its
result or messaging contracts. Validation brings FluentValidation as a package
dependency. AspNetCore references the `Microsoft.AspNetCore.App` shared
framework; it does not bring Runtime or Validation.

## Composition

Feature-specific registration is SliceForge's current composition mechanism;
there is intentionally no umbrella options/configuration API. Register Runtime
first, then handlers and validators, then decorators in the desired outer-to-inner
order:

```csharp
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSliceForgeCommandHandler<CreateTodoCommand, Guid, CreateTodoHandler>();
builder.Services.AddSliceForgeValidator<CreateTodoCommand, CreateTodoValidator>();
builder.Services.AddSliceForgeValidation();
builder.Services.AddSliceForgeObservability();
```

The resulting sender order is Observability → Validation → Runtime → handler.
Handler and validator registration is explicit; SliceForge does not scan
assemblies or select handlers polymorphically.

ASP.NET endpoints and success responses are application-owned. The AspNetCore
package's `ToHttpResult` maps expected errors, while the success callback can
return `Created`, `Ok`, or `NoContent` as the application requires. Consumers
configure `AddProblemDetails()` and `UseExceptionHandler()` directly. HTTP 500
is reserved for unexpected exceptions.

Observability uses built-in .NET instrumentation and Logging abstractions without
an OpenTelemetry dependency. Consumers own `service.name`, sampling, exporters,
and telemetry backends.

The packages are licensed under Apache-2.0. The CLI and template are preview
tools; expanded diagnostics remain future work. The template generates an
application that consumes the five runtime libraries above as NuGet packages.

## Install the CLI (preview)

Install the .NET global tool and use its compact root screen or commands:

```powershell
dotnet tool install --global SliceForge.Tool --version 0.1.0-preview.3
sliceforge
sliceforge new -n DispatchFlow
sliceforge doctor
```

The CLI delegates generation to `dotnet new sliceforge-api`; it does not own or
duplicate generated application architecture. If the template is missing,
interactive use asks before installing it. Redirected or CI use never prompts.
The `doctor` command only checks for an installed SDK, a .NET 10 SDK, and the
installed template; it does not install or restore anything.

## Generate an application (preview)

Install the template package and create a consumer application:

```powershell
dotnet new install SliceForge.Templates@0.1.0-preview.3
dotnet new sliceforge-api -n DispatchFlow
```

The direct template workflow remains available without the CLI. The generated
solution and projects use `DispatchFlow` as their identity and
include one small, removable example vertical slice. The template maps its
Minimal API endpoint explicitly and composes the five published SliceForge
library packages; it does not add repository project references or a CLI
dependency.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
