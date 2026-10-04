# SliceForge Architecture

SliceForge owns reusable application plumbing; consumer applications own business decisions. The repository uses separate packages to keep dependency boundaries explicit.

```text
Consumer application
        |
        ├── SliceForge.AspNetCore ──> SliceForge.Core
        │       └── Microsoft.AspNetCore.App framework reference
        ├── SliceForge.Runtime ─────> SliceForge.Core
        ├── SliceForge.Validation ──> SliceForge.Runtime (optional)
        └── SliceForge.Core
```

`SliceForge.Core` must remain independent of ASP.NET Core and dependency injection. It must not reference HTTP status codes, `HttpContext`, minimal API result types, ASP.NET middleware, or mediator libraries. Result-to-HTTP mapping belongs in `SliceForge.AspNetCore`; consumer applications own endpoint mapping and discovery is deferred.

`SliceForge.Validation` is optional and decorates the scoped Runtime sender with
explicit, exact-concrete-type FluentValidation routes. Runtime remains free of
validation dependencies.

`SliceForge.Observability` is optional and decorates the scoped Runtime sender
with structured message logs, `ActivitySource` activities, and `Meter`
metrics. When Validation is enabled, register Observability after Validation
so it remains outermost and sees validation outcomes. The package uses no
OpenTelemetry dependency; consumers subscribe to its instrumentation and own
resource identity (including `service.name`), exporters, sampling, and backend.

Optional instrumentation dependency direction:

```text
SliceForge.Observability → SliceForge.Runtime → SliceForge.Core
                         → Microsoft.Extensions.DependencyInjection.Abstractions
                         → Microsoft.Extensions.Logging.Abstractions
```

`SliceForge.AspNetCore` maps Core Results to ASP.NET Core `IResult` values. It
owns expected failure mapping while consumers choose success responses, map
endpoints explicitly, configure authentication, and register framework-owned
exception handling. The project references Core and the ASP.NET Core shared
framework only; it does not reference Runtime or Validation. Endpoint discovery
is deferred unless the Sample API demonstrates a concrete need.

## Configuration and package boundary

The completed configuration audit found no consumer-controlled setting that
justifies a shared options API. Explicit registration calls are the current
composition mechanism: `AddSliceForgeRuntime()`, `AddSliceForgeValidation()`,
and `AddSliceForgeObservability()`. Runtime is registered before decorators;
Validation precedes Observability when both are enabled.

The public package set is `SliceForge.Core`, `SliceForge.Runtime`,
`SliceForge.Validation`, `SliceForge.AspNetCore`, and
`SliceForge.Observability`. All are versioned together as `0.1.0-preview.1`
and target `net10.0`. Project references pack as NuGet dependencies; sibling
assemblies are not embedded. AspNetCore carries a `Microsoft.AspNetCore.App`
framework reference and has no Runtime dependency. The preview uses Apache-2.0
metadata and is validated from a local package feed by a clean consumer with no
source-project references. Validation does not publish packages.

`SliceForge.Templates` is a separate content-only NuGet template package at the
same preview version. It has no dependency on SliceForge libraries and embeds
no compiled assemblies. `dotnet new sliceforge-api -n DispatchFlow` generates
a consumer whose API project references the five libraries through NuGet,
whose versions are centrally managed in the generated application, and whose
single example feature demonstrates explicit endpoint-to-handler dispatch.
The template is validated by isolated installation, generation, package-feed
restore, build, and HTTP integration test. The CLI remains a separate future
tooling milestone.

Package dependencies are intentionally one-way:

```text
SliceForge.Core           (no package dependencies)
SliceForge.Runtime        -> Core + DI.Abstractions
SliceForge.Validation     -> Runtime + FluentValidation + DI.Abstractions
SliceForge.AspNetCore     -> Core + Microsoft.AspNetCore.App framework
SliceForge.Observability  -> Runtime + DI.Abstractions + Logging.Abstractions
```
