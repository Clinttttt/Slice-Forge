# SliceForge 0.1.0-preview.4 Release Readiness

**Review date:** 2026-11-05
**Status:** Ready with conditions; publication is not authorized.
**Audited source:** `11247981edec19c9fdb89129efae1f899b0cfd7f` on `main`
**Published baseline:** `0.1.0-preview.3` (`v0.1.0-preview.3`, commit `3fece42`)

This report records local source and package validation for a proposed coordinated
`0.1.0-preview.4` release. Repository versions remain `0.1.0-preview.3`.
Artifacts packed during this audit are local development packages and do not
replace or describe the already-published `preview.3` binaries.

## Release candidate scope

The five local commits after the `preview.3` tag add:

- Milestone 11A: opt-in, startup-only handler discovery from one caller-supplied
  assembly.
- Milestone 11B: opt-in validator discovery from one caller-supplied assembly,
  with deterministic ordering and the existing validation route store.
- Milestone 12: a generated application marker, discovered GET and validated
  POST slices, and explicit endpoint mapping.
- Milestone 13A: independently optional, default-on Validation and Observability
  template capabilities.
- Milestone 13B: interactive CLI selection and explicit non-interactive flags.

No changes to Core, ASP.NET Result mapping, or Observability implementation were
made after the `preview.3` source tag. The only new public library APIs in the
reviewed source diff are `AddSliceForgeHandlersFromAssembly(Assembly)` and
`AddSliceForgeValidatorsFromAssembly(Assembly)`. Existing public contracts and
registration methods remain present. This is a source-level API comparison; no
separate binary API compatibility tool was run.

## Package inventory

| Package ID | Source project | Release role | Target/package form | Dependencies |
|---|---|---|---|---|
| `SliceForge.Core` | `src/SliceForge.Core` | Results, errors, command/query contracts | `net10.0` library | None |
| `SliceForge.Runtime` | `src/SliceForge.Runtime` | Exact dispatch and handler registration/discovery | `net10.0` library | Core; `Microsoft.Extensions.DependencyInjection.Abstractions` |
| `SliceForge.Validation` | `src/SliceForge.Validation` | FluentValidation integration and validator discovery | `net10.0` library | Runtime; FluentValidation; DI abstractions |
| `SliceForge.AspNetCore` | `src/SliceForge.AspNetCore` | Result-to-HTTP mapping | `net10.0` library with ASP.NET shared-framework reference | Core; `Microsoft.AspNetCore.App` framework reference |
| `SliceForge.Observability` | `src/SliceForge.Observability` | Message logs, activities, and metrics | `net10.0` library | Runtime; DI and Logging abstractions |
| `SliceForge.Templates` | `templates/SliceForge.Templates` | Native `dotnet new sliceforge-api` template | `Template` package; content only | No package dependencies |
| `SliceForge.Tool` | `tools/SliceForge.Cli` | `sliceforge` global tool | `DotnetTool`; `net10.0` payload | System.CommandLine is bundled in the tool payload |

All packages inherit version, author, product, copyright, repository, license,
and README metadata from `Directory.Build.props`. The five libraries emit
separate symbol packages; the content-only template and CLI tool explicitly
disable separate symbol packages. Metadata uses author `Clint Villanueva`,
product `SliceForge`, repository type `git`, the SliceForge GitHub repository
URL, Apache-2.0, and the root README. Package IDs, descriptions, and tags are
package-specific. No company metadata is claimed.

## Public API and compatibility findings

- Core remains framework-independent: no ASP.NET Core, DI, FluentValidation, or
  Observability dependency was introduced.
- Runtime retains explicit command/query registration, transient handlers,
  scoped `IMessageSender`, exact `message.GetType()` route matching, and
  cancellation/result propagation. Discovery examines only the supplied
  assembly during registration; request dispatch does not scan or reflect.
- Validation retains explicit registration and its existing decorator, exact
  message matching, sequential multiple-validator execution, duplicate
  detection, failure aggregation, and active-scope validator resolution.
- ASP.NET Core keeps success response semantics with the endpoint. Expected
  failures map to the documented ProblemDetails/authentication results;
  unexpected exceptions remain exceptions.
- Observability decorates the sender and emits framework-native logs,
  activities, and metrics. It has no OpenTelemetry package dependency and must
  be registered after Validation to remain outermost.
- No accidental public helper types or package-boundary leaks were identified.

Both assembly scanners use reflection during startup and report type-loading
failures. They are **not claimed to be trimming- or Native-AOT-safe**. Consumers
requiring verified trimming/AOT behavior should use explicit handler and
validator registrations.

## Template and CLI compatibility

The template pins SliceForge package versions in its generated
`Directory.Packages.props`. Its baseline is Core, Runtime, and AspNetCore;
Validation and Observability are independently controlled by template
parameters and both default to `true`. FluentValidation is a direct generated
dependency only when Validation is selected. Endpoints remain explicitly
mapped, and the generated application uses the application-owned assembly
marker for handler and validator discovery.

All four direct-template combinations passed isolated restore, format, Release
build, and HTTP tests:

| Validation | Observability | Generated application | HTTP tests |
|---|---|---|---:|
| On | On | `DispatchFlow` (default) | 5 passed |
| On | Off | `ValidationOnlyFlow` | 5 passed |
| Off | On | `ObservabilityOnlyFlow` | 3 passed |
| Off | Off | `MinimalFlow` | 3 passed |

The installed local CLI tool passed its root/help/version/doctor checks and
generated the default `CliDispatchFlow`, both-disabled `CliMinimalFlow`, and
Validation-off/Observability-on `CliObservabilityFlow`; their generated HTTP
tests passed (5, 3, and 3 respectively). CLI unit tests exercise each positive
and negative switch, prompt behavior, conflicts, and forwarded template
arguments. Together with the direct-template matrix, the Validation-on /
Observability-off configuration is also verified. The CLI remains an
orchestrator; generation stays in the native template.

The published `preview.3` Runtime and Validation packages do not contain the
two discovery APIs. Therefore the published `preview.3` template/tool set is
not compatible with the current generated application source. The local pack
and consumer workflow validates current source-built packages against each
other; it does not establish compatibility with the published `preview.3`
artifacts.

## Packed package inspection

The audit packed the seven expected `.nupkg` files at the unchanged local
version `0.1.0-preview.3`, plus five `.snupkg` files for the library packages.
The package validator checked IDs, versions, metadata, README/license payloads,
dependency groups, XML documentation, source paths, and unwanted assemblies.
Direct archive inspection confirmed:

| Package | Generated dependency declaration / package contents |
|---|---|
| Core | No dependencies; only its own `net10.0` DLL/XML library payload |
| Runtime | Core `0.1.0-preview.3`; DI abstractions `10.0.12`; no embedded Core DLL |
| Validation | Runtime `0.1.0-preview.3`; FluentValidation `12.1.1`; DI abstractions `10.0.12`; no embedded Runtime/Core DLLs |
| AspNetCore | Core `0.1.0-preview.3`; `Microsoft.AspNetCore.App` framework reference for `net10.0`; no embedded Core DLL |
| Observability | Runtime `0.1.0-preview.3`; DI and Logging abstractions `10.0.12`; no embedded Runtime/Core DLLs |
| Templates | `PackageType=Template`; content-only payload; no compiled SliceForge assemblies or dependencies |
| Tool | `PackageType=DotnetTool`; command `sliceforge`; `net10.0` tool payload and bundled System.CommandLine `2.0.12`; no SliceForge runtime package dependency |

The generated nuspecs express internal SliceForge dependencies as bare versions,
for example `SliceForge.Core=0.1.0-preview.3` and
`SliceForge.Runtime=0.1.0-preview.3`. These are inclusive minimum constraints
with no upper bound. This existing pre-1.0 compatibility risk is recorded in
[ADR 0008](../decisions/0008-configuration-and-package-validation.md); custom
range rewriting was not added. The preview.4 package set must be coordinated,
and an owner decision is needed before a future incompatible package release.

Library `.nupkg` files contain their own DLL/XML documentation, README, and
LICENSE, with no sibling SliceForge assemblies, source files, or PDBs. Each
library has a corresponding `.snupkg` containing its PDB and Source Link URL;
the validator found no repository-local absolute paths. The content-only
template and global tool intentionally produce no separate `.snupkg`; the tool
includes its PDB in its executable payload. Package signing is not configured
and was not claimed or tested.

## Local validation and CI

`scripts/validate-packages.ps1` uses `artifacts/packages` as a local feed and
package-source mapping that routes `SliceForge.*` only to that feed. It clears
its repository-local package cache before consumer restore and falls back to a
new isolated cache if a prior cache is locked. The package-only consumer has no
SliceForge `ProjectReference`. Template installation uses a custom isolated
hive; CLI installation uses an isolated tool path and CLI home. Generated
projects are restored from the local feed plus nuget.org. The script does not
modify global template or tool installations. Cleanup verifies generated
targets are under the ignored `artifacts/package-validation` root.

The script expects exactly seven current-version SliceForge `.nupkg` files in
`artifacts/packages`; a stale package version in that feed intentionally fails
the count check. A release run should pack into a fresh/clean repository-local
package directory before validation.

The verification-only [CI workflow](../../.github/workflows/ci.yml) runs
checkout, SDK setup from `global.json`, restore, format verification, Release
build, all tests, pack, and isolated package/template/CLI validation on Ubuntu.
The validation script is invoked with PowerShell Core. No GitHub-hosted run was
observed for this local HEAD: it is five commits ahead of `origin/main`. Local
checks are not represented as hosted CI evidence.

The separate [publish workflow](../../.github/workflows/publish.yml) is
version-pinned to `0.1.0-preview.3` and triggers on any pushed `v*` tag. A
`v0.1.0-preview.4` tag at the current workflow state would enter the job and
fail its version check. Once the workflow is updated, pushing a matching tag
will proceed to trusted NuGet publishing of all seven packages. Updating and
using that workflow requires a separate approved release task; do not push a
release tag before that approval.

## Version migration checklist for an approved release task

Keep the seven package versions synchronized at `0.1.0-preview.4`. The
following source-controlled locations require coordinated review/update:

1. `Directory.Build.props`: set the shared project/package `Version` to
   `0.1.0-preview.4`. This also supplies the CLI assembly informational version
   used by version output and template install instructions.
2. `templates/SliceForge.Templates/content/sliceforge-api/Directory.Packages.props`:
   update the five conditional/baseline SliceForge package pins to preview.4.
3. `eng/package-validation/Directory.Packages.props`: update all five internal
   package pins to preview.4.
4. `scripts/validate-packages.ps1`: update `$expectedVersion` to preview.4;
   the generated package names, local install versions, and assertions derive
   from this value.
5. `.github/workflows/publish.yml`: update the job display name and `VERSION`
   to preview.4. Preserve its tag equality check and understand that the
   matching pushed tag triggers publication of all seven packages.
6. `tests/SliceForge.Cli.Tests/Commands/CliCommandTests.cs`,
   `DoctorCommandTests.cs`, and `NewCommandTests.cs`: update their expected
   version/install-command literals to preview.4.
7. Active user documentation: update package/tool/template installation
   examples and current-release statements in `README.md`,
   `docs/architecture.md`, `docs/roadmap.md`, and
   `docs/SLICEFORGE_IMPLEMENTATION_PLAN.md`. Preserve ADRs 0014/0015 as
   historical decisions; do not rewrite the fact that preview.3 lacked these
   later features.
8. Re-scan all version literals with `rg`, inspect generated template pins,
   build and pack the full set, inspect package names/nuspecs, and run isolated
   validation before any publication decision.

The following locations already derive or are intentionally version-independent:

- `TemplateIdentity.Version`, CLI `version`/`--version`, root screen, and
  `TemplateIdentity.InstallCommand` derive from assembly informational version;
  no CLI source version constant needs changing.
- `Directory.Packages.props` contains external dependency versions, not the
  coordinated SliceForge package version.
- `global.json` pins the .NET SDK (`10.0.401`); it is not a package version.
- `.github/workflows/ci.yml` and the package-validation consumer installation
  commands derive their package versions from the validation script's
  `$expectedVersion`.

## Verification performed

| Check | Result |
|---|---|
| `dotnet restore SliceForge.slnx` | Passed; all projects up to date |
| `dotnet format SliceForge.slnx --verify-no-changes` | Passed |
| `dotnet build SliceForge.slnx -c Release` | Passed; 0 warnings, 0 errors |
| `dotnet test SliceForge.slnx -c Release` | Passed; 200 passed, 0 failed, 0 skipped |
| `dotnet pack SliceForge.slnx -c Release --no-restore -o artifacts/packages` | Passed; 7 `.nupkg` and 5 library `.snupkg` files |
| `./scripts/validate-packages.ps1` | Passed; package inspection, package-only consumer, all four template combinations, isolated tool install, and CLI generation |
| `git diff --check` | Passed after adding this report |

Pack emitted the two expected non-fatal warnings that the sample API and sample
API test projects have packaging disabled. These are not compiler warnings.

## Limitations and findings

### Blocking before publication

- The source and local packages are still labeled `0.1.0-preview.3`, while the
  published preview.3 binaries lack handler/validator discovery. The approved
  coordinated version migration is required before publishing this source as
  preview.4.
- The publish workflow still targets preview.3. It must be updated and reviewed
  in the approved release task. Its tag trigger is a publication action, not a
  passive release marker.
- Hosted CI has not been observed for this local HEAD. Run the required CI on
  the reviewed, remotely available release candidate before publication.

### Non-blocking for this additive preview

- Internal NuGet dependency constraints are unbounded inclusive minimums, as
  recorded above and in ADR 0008.
- Handler/validator assembly discovery has reflection-based trimming/AOT
  limitations; explicit registration is the documented alternative.
- The template uses explicit endpoint mapping. Endpoint assembly discovery is
  not part of SliceForge.
- Only `net10.0` is targeted. This is the current preview support contract.
- Package signing is not configured. No signature or cryptographic verification
  is claimed.

## Publication checklist for a later approved release

1. Complete the coordinated version migration and independently review the
   resulting source diff and seven-package scope.
2. Restore, format-check, Release-build, test, pack, inspect, and validate all
   seven release-candidate packages, including the isolated consumer and
   generated template/CLI applications.
3. Push the reviewed release-candidate commit only with separate approval.
4. Verify the hosted GitHub Actions CI run for that candidate commit.
5. Obtain explicit approval for public NuGet publication.
6. Create and push the exact approved release tag, `v0.1.0-preview.4`.
7. Allow the existing tag-triggered workflow to publish the coordinated
   package set. **Pushing a matching release tag triggers publication; it must
   never be treated as a passive validation action.**
8. Monitor the workflow and verify publication of all seven packages.
9. Install the published packages from a clean public NuGet feed and run
   generated-project smoke tests.
10. Complete release notes and other release documentation.

This readiness report does not authorize a version bump, release tag, GitHub
release, NuGet publication, or push.
