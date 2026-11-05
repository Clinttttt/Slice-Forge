# SliceForge

SliceForge is a composable Vertical Slice toolkit for modern .NET applications.
It provides Result-based application flows, explicit command/query dispatch,
validation, ASP.NET Core integration, observability, project templates, and CLI
tooling without requiring MediatR. The latest public NuGet release remains
`0.1.0-preview.3`, targeting .NET 10. This checkout is being prepared as an
unpublished, coordinated `0.1.0-preview.4` candidate; APIs and package
boundaries may still evolve before a stable release.

**Candidate compatibility:** the current template source uses handler and
validator discovery APIs that are not present in the published `preview.3`
library binaries. Its package pins now target the local `preview.4` candidate.
Package validation rebuilds the seven packages and maps `SliceForge.*` IDs to
that local feed. The candidate is not available from NuGet.org and must ship
only as a coordinated release containing those APIs.

## Packages

| Package | Responsibility |
|---|---|
| `SliceForge.Core` | Framework-independent results, errors, and messaging contracts. |
| `SliceForge.Runtime` | Exact command/query routing, explicit registration, and optional startup discovery from a supplied assembly. |
| `SliceForge.Validation` | Optional FluentValidation sender decorator. |
| `SliceForge.AspNetCore` | Expected Result-to-HTTP mapping; success responses remain application-owned. |
| `SliceForge.Observability` | Optional .NET logging, activity, and metrics instrumentation. |
| `SliceForge.Templates` | A `dotnet new` template for a .NET 10 Minimal API consumer. |
| `SliceForge.Tool` | A .NET global tool for creating applications from the template and checking the local environment. |

The following installs are for the currently published `preview.3` packages.
Install only the capabilities your application uses:

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
var applicationAssembly = typeof(ApplicationAssemblyMarker).Assembly;
builder.Services.AddSliceForgeHandlersFromAssembly(applicationAssembly);
builder.Services.AddSliceForgeValidatorsFromAssembly(applicationAssembly);
builder.Services.AddSliceForgeValidation();
builder.Services.AddSliceForgeObservability();
```

The resulting sender order is Observability → Validation → Runtime → handler.
Handlers may instead be registered individually with the existing explicit
methods. Assembly discovery is opt-in, scans only the supplied assembly during
registration, and does not change exact concrete-message routing. Validator
discovery is a separate opt-in API over the same caller-supplied assembly; it
scans closed `IValidator<TMessage>` contracts at registration time only.
Discovered validators are ordered by fully qualified type name per message and
appended at the point discovery is registered, so explicit registrations before
or after discovery retain their relative order. Explicit validator registration
remains supported. Neither scanner discovers endpoints or scans at request time.

Define the marker type in the application assembly that contains the handlers
you want Runtime to discover:

```csharp
public sealed class ApplicationAssemblyMarker { }
```

ASP.NET endpoints and success responses are application-owned. The AspNetCore
package's `ToHttpResult` maps expected errors, while the success callback can
return `Created`, `Ok`, or `NoContent` as the application requires. Consumers
configure `AddProblemDetails()` and `UseExceptionHandler()` directly. HTTP 500
is reserved for unexpected exceptions.

Observability uses built-in .NET instrumentation and Logging abstractions without
an OpenTelemetry dependency. Consumers own `service.name`, sampling, exporters,
and telemetry backends.

The packages are licensed under Apache-2.0. The CLI and template are preview
tools; expanded diagnostics remain future work. The development template
includes Core, Runtime, and AspNetCore by default, with optional Validation and
Observability package references selected at generation time.

## Install the CLI (preview)

Install the .NET global tool and use its compact root screen or commands:

The published `preview.3` CLI does not include the capability wizard or flags.
The following candidate commands are for use after the coordinated `preview.4`
release is published; that release has not happened.

```powershell
dotnet tool install --global SliceForge.Tool --version 0.1.0-preview.4
sliceforge
sliceforge new -n DispatchFlow
sliceforge new -n DispatchFlow --validation --observability
sliceforge new -n DispatchFlow --no-validation --observability
sliceforge new -n DispatchFlow --non-interactive
sliceforge doctor
```

The CLI delegates generation to `dotnet new sliceforge-api`; it does not own or
duplicate generated application architecture. On an interactive terminal,
`new` asks whether to include Validation and Observability, shows the resulting
package selection, then asks for confirmation before checking or generating the
template. Both capabilities default to enabled. `--validation` /
`--no-validation` and `--observability` / `--no-observability` choose them
independently; a supplied capability flag skips the wizard, and conflicting
positive/negative flags are rejected. `--non-interactive` disables all prompts
and requires a name. Redirected input also skips prompts and uses template
defaults unless capability flags are supplied. The CLI forwards choices to the
native template parameters; it never edits generated files itself. If a
template is missing, installation is asked only when prompting is allowed.
`doctor` checks for an installed SDK, a .NET 10 SDK, and the installed template;
it does not install or restore anything.

These capability questions and flags are implemented in the local
`preview.4` candidate, not in the published `preview.3` CLI. The published
libraries also predate the discovery APIs used by the candidate template.

## Generate an application (preview)

The currently published `preview.3` template can be installed with:

```powershell
dotnet new install SliceForge.Templates@0.1.0-preview.3
dotnet new sliceforge-api -n DispatchFlow
```

The updated candidate template and its generated `preview.4` package pins are
not available from NuGet.org yet. After the coordinated release, install it
and create a consumer application with:

```powershell
dotnet new install SliceForge.Templates@0.1.0-preview.4
dotnet new sliceforge-api -n DispatchFlow
```

**Candidate template options (not yet published):** `UseValidation` and
`UseObservability` are independent boolean options, each enabled by default.
For a minimal API without either optional capability:

```powershell
dotnet new sliceforge-api -n DispatchFlow --UseValidation false --UseObservability false
```

Each `false` removes its package reference and central package pin, namespace
import, and DI registration; disabling Validation also removes its direct
FluentValidation dependency, generated validator source, and switches the
generated HTTP test to demonstrate that no automatic name validation occurs.
The always-on baseline is Core, Runtime, and AspNetCore. Enabling Validation adds pre-handler input rules; disabling it
does **not** provide alternative request validation or change domain invariants.
Observability remains optional and does not automatically configure a telemetry
exporter. All four combinations are exercised by isolated generated-app tests.

The direct template workflow remains available without the CLI. The generated
solution and projects use `DispatchFlow` as their identity and include a small,
removable GET query slice and a greeting command slice. An application-owned
assembly marker enables startup-only handler discovery and, when selected,
validator discovery; endpoints remain mapped explicitly. Package references
point to NuGet, not repository projects, and the generated API has no CLI
dependency. Both optional capabilities are enabled by default. The current CLI
source can select either independently; selection controls template-owned
packages, registrations, and validator files. A generation with neither
capability contains only Core, Runtime, and AspNetCore.

## Build and test

```powershell
dotnet restore
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

The repository targets .NET 10 and treats compiler and analyzer warnings as errors.
