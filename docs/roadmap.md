# SliceForge Roadmap

The implementation is intentionally incremental:

1. Repository foundation — completed
2. Core result and error model — completed
3. Messaging abstractions — completed
4. Runtime execution and explicit handler registration — completed
5. Validation — completed
6. ASP.NET Core Result mapping — completed
7. Sample consumer API — completed
8. Logging and observability — completed
9. Configuration audit — completed with no configuration API
10. Package validation — completed and exercised against published preview packages
11. `dotnet new` template — completed as a preview package
12. `SliceForge.Cli` global tool — completed as a preview package
13. Diagnostics — next
14. Real-world adoption

Milestone 5 adds Core-only Result-to-HTTP mapping in `SliceForge.AspNetCore`.
Consumers own successful response semantics, explicit endpoint mapping,
authentication configuration, and ASP.NET Core exception middleware setup.
Endpoint discovery remains deferred. Validation is an optional sender
decorator outside Core and Runtime; Runtime itself remains free of validation,
logging, and mediator dependencies.

Milestone 6 is demonstrated by the Todos Sample API. Its explicitly mapped
Minimal API routes exercise typed command, non-generic command, and query
dispatch; FluentValidation through the optional sender decorator; and
consumer-owned Created/OK/NoContent responses. A test-only
`Microsoft.AspNetCore.Mvc.Testing` 10.0.12 project verifies the complete HTTP
flow with a fresh in-memory store per test.

Milestone 7 adds the optional `SliceForge.Observability` sender decorator. It
emits structured outcome logs, command/query activities, and low-cardinality
execution metrics using .NET instrumentation primitives. It does not depend on
OpenTelemetry; consumers configure subscriptions, `service.name`, exporters,
and sampling. Register it after Validation so it observes validation failures.
Milestone 8 closes the configuration audit without adding an options API and
validates the five synchronized `0.1.0-preview.3` libraries through a local
feed and isolated package-only consumer. CI repeats restore, formatting, build,
tests, pack, package inspection, and consumer execution; it never publishes.

Milestone 9 adds the content-only `SliceForge.Templates` NuGet package. It
generates a .NET 10 Minimal API and test solution from a neutral placeholder,
using package references rather than SliceForge source projects. Its single
removable example slice demonstrates explicit endpoint, sender, handler,
Result, and HTTP mapping. CI installs it in an isolated template environment,
generates `DispatchFlow`, and restores, builds, and tests that generated
application from the local package feed. Endpoint discovery remains deferred.

Milestone 10 adds the `SliceForge.Cli` implementation as the `SliceForge.Tool` .NET global-tool package, without references to
the SliceForge runtime packages. It provides a restrained root screen, standard
help/version parsing, `new`, `doctor`, and `version`; generation delegates to
the installed `sliceforge-api` template. Template installation is explicit and
requires interactive confirmation when needed. Doctor performs only local SDK
and template checks and does not install or restore anything. Package
validation installs the tool and template into isolated locations, exercises
the commands, generates `CliDispatchFlow`, and restores/builds/tests that
consumer using the local package feed. Nothing is published.
