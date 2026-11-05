# ADR 0015: CLI Capability Selection

## Status

Accepted

## Context

Milestone 13A made Validation and Observability independently selectable in the
native `sliceforge-api` template, with both enabled by default. Exposing those
choices in the CLI must not create a second generator or make script execution
depend on terminal input.

## Decision

The CLI owns capability interaction and process orchestration; the template
continues to own all generated files and package composition. On an interactive
`sliceforge new` invocation without capability flags, the CLI asks about each
capability, shows the selected packages, and requires confirmation before
template lookup or generation. The template defaults remain Validation on and
Observability on, as established by ADR 0014.

The presence-based `--validation` / `--no-validation` and `--observability` /
`--no-observability` switches select capabilities independently. A contradictory
pair is an invalid invocation (exit code 2) and causes no prompt or SDK call.
Any capability flag requires an explicit project name and suppresses prompts.
`--non-interactive` also requires a name and suppresses all prompts; redirected
input follows the same no-prompt rule. Unspecified choices in unattended use
remain the template defaults. Explicit or interactively selected values are
forwarded as the template's existing `UseValidation` and `UseObservability`
parameters. Missing templates are installed only after user confirmation when
interaction is allowed.

## Consequences

- The command remains testable through the existing console and process seams.
- CI and scripts can generate deterministically without hanging on prompts.
- Capability selection removes or includes the template-owned package pins,
  registrations, and source files; the CLI never edits generated files.
- The CLI and template changes are not part of the published
  `0.1.0-preview.3` artifacts and require a coordinated release with the
  discovery APIs used by the generated application.

## Rejected alternatives

- A CLI-side project generator was rejected because it duplicates the native
  template and can drift from direct `dotnet new` usage.
- Changing Observability's default to off was rejected because it conflicts
  with the accepted template contract in ADR 0014.
