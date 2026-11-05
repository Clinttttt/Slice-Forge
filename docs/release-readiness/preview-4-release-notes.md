# SliceForge 0.1.0-preview.4 — Draft Release Notes

**Status:** Draft for an unreleased candidate. `0.1.0-preview.4` is not
available from NuGet.org.

This coordinated preview adds the following capabilities since the published
`0.1.0-preview.3` release.

## Handler and validator discovery

Runtime adds opt-in startup discovery through
`AddSliceForgeHandlersFromAssembly(assembly)`. Validation adds the separate
`AddSliceForgeValidatorsFromAssembly(assembly)` API. Each scans only the
explicitly supplied assembly during service registration and reuses the
existing routing or validation registration infrastructure. Explicit
registration remains available.

These scanners use reflection. Trimming and Native AOT compatibility have not
been established; use explicit registration when those deployment modes
require predictable preservation.

## Updated application template

The `sliceforge-api` template includes an application-owned assembly marker,
handler and validator discovery, a GET query example, and a validated greeting
command. Endpoints remain explicitly mapped, and expected validation failures
map through SliceForge Results to HTTP 400.

Validation and Observability are independent template capabilities, both
enabled by default. Disabling either removes its package references,
registrations, and capability-specific generated content. Validation also
controls the generated FluentValidation reference and validator source.

## CLI capability selection

The `sliceforge new` command provides interactive Validation and Observability
selection, the `--validation`, `--no-validation`, `--observability`, and
`--no-observability` switches, and `--non-interactive`. Both capabilities
default to enabled. The CLI forwards choices to the native template and does
not generate application files itself.

## Scope and limitations

- Targets .NET 10 only.
- Endpoint mapping remains explicit; endpoint discovery is not included.
- No authentication, Swagger/OpenAPI, persistence, or other future capability
  scaffolding is included.
- Observability registration does not configure exporters, sampling, or a
  telemetry backend.
- Preview APIs and package compatibility may change before a stable release.
- All seven packages must be released together because the candidate template
  uses discovery APIs absent from the published `preview.3` Runtime and
  Validation packages.
