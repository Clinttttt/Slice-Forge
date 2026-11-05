# SliceForge 0.1.0-preview.4 Candidate Verification

**Status:** Locally verified candidate; ready for independent review, not
approved for publication.
**Candidate source baseline:** `main` at `43f42229b4ffc42bae94c68911e3205fa47e1bde`;
the coordinated candidate preparation is recorded by this report and its commit.
**Published baseline:** `0.1.0-preview.3` (`v0.1.0-preview.3`).

Nothing in this candidate has been published. The coordinated package version
is `0.1.0-preview.4` in source and local artifacts only.

## Version migration

The shared version in `Directory.Build.props`, all five generated template
library pins, all five package-only consumer pins, the package validator's
`$expectedVersion`, the publish workflow's display name and `VERSION`, and the
CLI version/install-command test expectations now use `0.1.0-preview.4`.
External dependency versions and `global.json` are unchanged. CLI production
version reporting continues to derive from assembly informational metadata.

Remaining `preview.3` references describe the currently published public
baseline, working installation commands for that public baseline, historical
milestone/ADR statements, or the pre-migration audit in
[preview-4-readiness.md](preview-4-readiness.md). That prior audit was not
rewritten. No active source version pin remains at `preview.3`.

## Candidate package inventory

All seven packages target the synchronized candidate and retain their package
IDs. Package validation confirmed shared author, product, Apache-2.0 license,
repository, README, and package-specific metadata. The local feed contains
exactly seven `.nupkg` files and five library `.snupkg` files. The template and
tool have no separate symbol packages.

| Package | Package type / role | Generated dependency declarations |
|---|---|---|
| `SliceForge.Core` | `net10.0` library | None |
| `SliceForge.Runtime` | `net10.0` library | Core `0.1.0-preview.4`; DI abstractions `10.0.12` |
| `SliceForge.Validation` | `net10.0` library | Runtime `0.1.0-preview.4`; FluentValidation `12.1.1`; DI abstractions `10.0.12` |
| `SliceForge.AspNetCore` | `net10.0` library | Core `0.1.0-preview.4`; ASP.NET Core shared-framework reference |
| `SliceForge.Observability` | `net10.0` library | Runtime `0.1.0-preview.4`; DI and Logging abstractions `10.0.12` |
| `SliceForge.Templates` | `Template` content package | None |
| `SliceForge.Tool` | `DotnetTool`, command `sliceforge` | No SliceForge package dependency; System.CommandLine is in the tool payload |

The generated internal dependency constraints retain the existing bare-version
policy: NuGet treats these as inclusive minimums without an upper bound. This
known pre-1.0 compatibility risk remains as recorded in ADR 0008. Package
inspection found no stale `.3` artifacts in the active local feed, no embedded
sibling library assemblies, and no unexpected source payloads. The five
library symbol packages contain Source Link metadata and no repository-local
absolute paths. Signing is not configured and is not claimed.

## Verification results

| Check | Result |
|---|---|
| `dotnet restore SliceForge.slnx` | Passed |
| `dotnet format SliceForge.slnx --verify-no-changes` | Passed |
| `dotnet build SliceForge.slnx -c Release --no-restore` | Passed; 0 warnings, 0 errors |
| `dotnet test SliceForge.slnx -c Release --no-build` | Passed; 200 passed, 0 failed, 0 skipped |
| `dotnet pack SliceForge.slnx -c Release --no-restore -o artifacts/packages` | Passed; 7 `.nupkg`, 5 `.snupkg` |
| `./scripts/validate-packages.ps1` | Passed; package inspection, package-only consumer, template matrix, installed CLI, and generated applications |

Pack reported two non-fatal warnings because the Sample API and its test
project intentionally have packaging disabled. These are not compiler
warnings.

The isolated package-only consumer restored against the local `SliceForge.*`
feed plus nuget.org, built with zero warnings/errors, and passed its execution
smoke. Its configuration has no SliceForge project or assembly references.
Package-source mapping directs SliceForge IDs only to the freshly packed
candidate feed; the user's global NuGet cache was not cleared.

### Generated template matrix

Each direct-template application restored the local candidate packages, passed
format verification, built in Release, and passed its HTTP tests.

| Validation | Observability | Application | HTTP tests |
|---|---|---|---:|
| On | On | `DispatchFlow` | 5 passed |
| On | Off | `ValidationOnlyFlow` | 5 passed |
| Off | On | `ObservabilityOnlyFlow` | 3 passed |
| Off | Off | `MinimalFlow` | 3 passed |

### Installed CLI matrix

The packed `.4` tool and template were installed using isolated locations. The
tool's root screen, help, `new --help`, `--version`, `version`, and `doctor`
checks passed; both version commands reported exactly `0.1.0-preview.4`.

| Selection | Application | HTTP tests |
|---|---|---:|
| Default (both enabled) | `CliDispatchFlow` | 5 passed |
| Validation off, Observability off | `CliMinimalFlow` | 3 passed |
| Validation off, Observability on | `CliObservabilityFlow` | 3 passed |

All three were generated by the installed tool, restored against the local
candidate feed, built in Release, and passed their HTTP tests. Template and
CLI-generated applications use the requested project identities and candidate
package references without SliceForge source-project references.

## Workflow and publication status

The verification-only CI workflow remains unchanged. The publish workflow now
names and checks version `0.1.0-preview.4`; its `v*` tag trigger, tag equality
guard, seven-package list, trusted-publishing login, publication order, and
`--skip-duplicate` behavior remain unchanged. The matching tag
`v0.1.0-preview.4` triggers the actual publication workflow and is not a
validation-only action.

No hosted GitHub Actions run for this candidate was observed. Local checks do
not substitute for a hosted run on the separately reviewed and approved
candidate commit. No publish command, release tag, push, or public package
publication occurred.

## Conditions before publication

- Independently review the candidate source and package artifacts.
- Commit and push the reviewed candidate only after separate approval, then
  verify hosted CI for that commit.
- Obtain explicit publication approval before creating and pushing the exact
  release tag; the tag starts publication of the seven packages.
- Monitor publication and verify all seven public artifacts, then install from
  a clean public feed and smoke-test generated applications.
- Treat partial NuGet publication as a release incident; publication is not
  atomic across packages.

The local candidate is technically verified and safe to proceed to independent
review. It is not yet eligible for public installation or publication.
