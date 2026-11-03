# ADR 0014: Optional Template Validation and Observability

## Status

Accepted

## Context

The `sliceforge-api` template originally generated a fixed set of five
SliceForge library references and registered both optional message decorators.
The application examples introduced in Milestone 12 demonstrate handler and
validator assembly discovery, but requiring an unused capability conflicts
with SliceForge's composable package boundaries.

A generator must omit dependencies and source files for excluded capabilities,
not merely disable their behavior at runtime. The CLI remains a thin wrapper
around the `dotnet new` template and must not become a second code generator.

## Decision

The template exposes independent `UseValidation` and `UseObservability`
boolean parameters, both `true` by default to preserve existing generated
application behavior.

The native .NET template engine selects the generated content:

- The baseline always includes Core, Runtime, and AspNetCore, one
  application-owned assembly marker, handler assembly discovery, and explicit
  GET and POST endpoint mapping.
- `UseValidation=true` includes SliceForge.Validation and an explicit
  FluentValidation package reference because generated code directly uses its
  APIs, the validator discovery and sender decorator calls, the greeting
  validator source, and validation-specific HTTP tests.
- `UseValidation=false` excludes Validation and FluentValidation direct
  package references, their central version entries, import and registration
  calls, and the greeting validator source. Its generated HTTP test demonstrates
  that no pre-handler name-validation rule is installed.
- `UseObservability=true` includes SliceForge.Observability and its
  composition-root registration call.
- `UseObservability=false` omits its package reference, version entry,
  import, and registration call.

Template conditional directives and a source modifier implement this without
a separate file generator. No optional package is introduced transitively
through the application template when the corresponding option is disabled.
Validation still runs before Observability when both are selected.

The default `sliceforge new` CLI behavior remains unchanged; interactive
capability prompts and non-interactive CLI flags are separately planned.

## Consequences

- Each of the four option combinations restores, formats, builds, and runs
  generated-app tests from isolated locally packed SliceForge packages.
- Validation is not replaced by another automatic input-validation system
  when disabled; applications continue to own domain invariants and any
  additional HTTP input checks.
- All configurations retain the same pure Vertical Slice architecture,
  exact handler routing, explicit endpoint mapping, and application-owned
  HTTP success semantics.
- Source-level template options are not yet available in the published
  `0.1.0-preview.3` template, whose published Runtime and Validation
  binaries also predate discovery. A coordinated release is required before
  publicly distributing the updated template.
- Future capabilities can use the same approach only when their optional
  dependencies, configuration, tests, and removal behavior are defined.

## Rejected alternatives

- Keep package references while disabling a decorator: rejected because
  unnecessary dependencies and version pins remain in the generated app.
- Generate a validator when Validation is disabled: rejected because it
  cannot execute through the omitted Validation package.
- Generate architecture directly from the CLI: rejected because it would
  duplicate template responsibility established in ADR 0010.
- Add a runtime options/configuration framework: rejected because the
  selection is a project-generation concern, not a runtime setting.

This ADR extends ADR 0009's template decision and ADRs 0012–0013's
assembly discovery decisions without changing their underlying API or
execution semantics.
