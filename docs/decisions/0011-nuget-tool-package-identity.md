# NuGet tool package identity and preview recovery

Status: Accepted

## Context

The first automated NuGet.org publication attempt used the package ID
`SliceForge.Cli` for the .NET global tool. NuGet.org already contains an
unrelated package with that ID, so trusted publishing correctly rejected the
tool package. The workflow had already uploaded
`SliceForge.AspNetCore 0.1.0-preview.1` before the collision stopped the
coordinated package set.

The implementation project, assembly, namespaces, and executable command are
already established as `SliceForge.Cli` and `sliceforge`. Only the NuGet
package identity needs to change.

## Decision

The public NuGet package ID for the global tool is `SliceForge.Tool`.

The implementation project and assembly remain `SliceForge.Cli`, and the
installed command remains:

```text
sliceforge
```

The first complete coordinated public preview is bumped to
`0.1.0-preview.2`. This avoids pretending the partially published
`0.1.0-preview.1` set was complete and preserves release provenance without
moving or rewriting the existing `v0.1.0-preview.1` tag.

The trusted publishing workflow publishes the coordinated set in dependency
order and names each package explicitly rather than pushing every `.nupkg`
found in the artifacts directory.

## Consequences

- Users install the tool with
  `dotnet tool install --global SliceForge.Tool --version 0.1.0-preview.2`.
- The tool command remains `sliceforge`; no application-facing CLI syntax
  changes.
- `SliceForge.Cli` remains the source project/assembly name and is not a NuGet
  package ID owned by this project.
- All seven intended public packages use `0.1.0-preview.2` for the first
  complete coordinated preview.
- The already uploaded `SliceForge.AspNetCore 0.1.0-preview.1` is treated as
  an incomplete preview artifact and is superseded by `0.1.0-preview.2`.
