# ADR 0009: Content-Only .NET Application Template

- Status: Accepted
- Date: 2026-10-04

## Context

The five SliceForge runtime libraries and package-only consumer validation are
complete. Consumers need a scriptable way to start an application without
copying repository source or requiring the future interactive CLI. The template
must demonstrate the released public package APIs while keeping application
identity, endpoint mapping, and success semantics consumer-owned.

## Decision

Add `SliceForge.Templates` as a content-only NuGet package with
`PackageType=Template`, synchronized version `0.1.0-preview.1`, and .NET 10
template content. It depends on no SliceForge package and contains no compiled
runtime assemblies. Install and generate with:

```powershell
dotnet new install SliceForge.Templates@0.1.0-preview.1
dotnet new sliceforge-api -n DispatchFlow
```

Template source uses the neutral `TemplateApp` replacement token. Generated
solution, project names, paths, and namespaces derive from the consumer-supplied
name. Author attribution remains package metadata and is not generated into
consumer code.

The generated application contains a Minimal API project and HTTP integration
test project, plus one small removable `GetExample` vertical slice. It
references the five SliceForge libraries through NuGet PackageReferences,
centrally pins their synchronized versions in generated
`Directory.Packages.props`, explicitly maps its endpoint, and composes Runtime,
Validation, and then Observability. ASP.NET Core owns ProblemDetails and
exception middleware configuration. The template does not include database,
authentication, repository, endpoint-discovery, or CLI infrastructure.

Package validation installs the packed template under an isolated
`DOTNET_CLI_HOME`, generates a named consumer, verifies its package/project
boundaries, and restores, builds, and runs its HTTP test using only the local
SliceForge package feed and nuget.org. CI runs the same validation on Ubuntu.
The workflow does not publish packages.

## Consequences

- Direct `dotnet new` usage is supported independently of the future CLI.
- The generated app proves the current public APIs as an external package
  consumer and can expose usability friction without creating sample-only
  SliceForge APIs.
- The template package is content-only and has no SliceForge runtime dependency
  or `.snupkg` symbol output.
- Template installation and generated-app restore/build/test become package
  validation gates; a source build alone is insufficient.
- The Todos Sample API remains the repository's realistic integration example;
  the generated default is domain-neutral.
- The package remains a preview and is not published by repository CI.
- The interactive CLI remains a separate future package that may orchestrate
  this template rather than duplicating its generated architecture.

## Alternatives considered

- Generating application files from the CLI was rejected because it would make
  scriptable `dotnet new` usage unavailable and duplicate architecture in two
  distribution mechanisms.
- Compiling template content into a SliceForge runtime assembly was rejected;
  the SDK template engine consumes package content and does not need runtime
  library dependencies.
- A Todos default was rejected because it would make generated application
  identity and domain assumptions unnecessarily specific.
- Endpoint scanning, database layers, and additional generated slices were
  rejected as outside the smallest useful consumer example.
