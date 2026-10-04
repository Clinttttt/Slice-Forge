# ADR 0008: Configuration Audit and Package Validation

- Status: Accepted
- Date: 2026-10-04

## Context

SliceForge has completed Core results and messaging contracts, explicit Runtime
routing, optional Validation and Observability sender decorators, and an
ASP.NET Core result-mapping package. The Todos Sample API composes these
features with explicit registration. Before templates or CLI work, the
repository needs a real NuGet package-consumption check.

The current sample does not demonstrate a consumer-controlled setting that
would justify a shared `SliceForgeOptions`, umbrella `AddSliceForge`, or
feature-option model. Packaging all libraries also introduces version,
license, metadata, symbol, and package-boundary decisions that should be
consistent and testable before distribution.

## Decisions

### Configuration

The configuration audit is closed without a production configuration API.
Feature-specific registration is the composition mechanism:

```csharp
services.AddSliceForgeRuntime();
services.AddSliceForgeValidation();
services.AddSliceForgeObservability();
```

Runtime is registered before decorators. Validation is registered before
Observability when both are enabled. New options may be added only when a
consumer demonstrates a specific setting that cannot be expressed clearly by
existing explicit registration. No placeholder booleans, database/auth options,
or endpoint-discovery settings are added.

### Public package set and compatibility

The initial package set is:

- `SliceForge.Core`
- `SliceForge.Runtime`
- `SliceForge.Validation`
- `SliceForge.AspNetCore`
- `SliceForge.Observability`

All five packages use the synchronized version `0.1.0-preview.1` and target
`net10.0` only. Their NuGet PackageIds match their project names. Project
references become NuGet dependencies; sibling assemblies are not embedded.
The dependency boundaries remain unchanged, including AspNetCore's
`Microsoft.AspNetCore.App` framework reference and lack of Runtime dependency.

### Metadata and license

Shared metadata is centralized in repository build configuration where
appropriate: author `Clint Villanueva`, product `SliceForge`, repository type
`git`, repository URL, synchronized version, and Apache-2.0 license expression.
Descriptions and tags remain package-specific. No company metadata is claimed.
The root README is included in each package for this preview, alongside the
canonical root license text. No public publishing is part of this decision.

XML API documentation is generated for the five libraries. Symbol packages use
the `.snupkg` format. Repository/source metadata uses the current .NET SDK's
Source Link support; no Source Link package is added unless SDK behavior proves
insufficient. Continuous-integration build metadata is enabled in CI only.

### Required local package validation

Before templates or CLI, release validation must:

1. Pack all five libraries into the ignored repository-local
   `artifacts/packages` feed.
2. Inspect each `.nupkg` and matching `.snupkg` for identity, version, own
   assembly and XML docs, README, license/repository metadata, framework and
   package dependencies, and absence of tests, samples, source files, embedded
   sibling SliceForge assemblies, PDBs in normal packages, and local paths.
3. Restore, build, and run a committed package-consumer fixture materialized
   under `artifacts/package-validation` using only the local packages and
   nuget.org, with isolated build/package configuration and no project/source
   references.
4. Run the same checks in CI without publishing credentials or publishing.

This verifies package behavior as an external consumer sees it, including the
ASP.NET shared framework reference and the Runtime → Validation → Observability
composition. A successful source-project build alone is not package
validation.

## Consequences

- The current feature registrations remain the complete configuration API.
- Package releases remain synchronized and target only .NET 10 for this preview.
- Package-to-package dependency ranges are inspected as generated; custom
  dependency-range rewriting is not introduced without a separate decision.
- The current generated nuspecs express SliceForge package dependencies as the
  bare version `0.1.0-preview.1`, which NuGet interprets as an inclusive
  minimum with no upper bound. That permits a later incompatible 0.x version
  to satisfy the dependency. This risk is recorded rather than addressed with
  custom pack logic in this milestone; a bounded pre-1.0 dependency policy
  requires a separate owner decision before subsequent incompatible package
  releases.
- The Apache-2.0 choice is expressed with NuGet's license expression metadata
  and accompanied by the canonical license text.
- Package validation is local and repeatable; it does not imply or authorize a
  NuGet.org release.
- Template and CLI work must not rely on source-project references as a
  substitute for package-consumer validation.

## Alternatives considered

- A shared options object was rejected because the Sample API demonstrated no
  setting requiring one; it would create unsupported configuration surface.
- Per-package versions were rejected in favor of synchronized preview
  versioning so the dependency set is coherent and reviewable.
- Machine-specific local feeds and manual package installation were rejected
  in favor of ignored repository-local output and automated isolated restore,
  build, and execution.
- Source Link package references were rejected while the .NET SDK supplies the
  required GitHub repository integration.
