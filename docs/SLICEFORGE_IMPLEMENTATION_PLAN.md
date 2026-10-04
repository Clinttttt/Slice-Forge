# SliceForge — Implementation Plan & Agent Execution Guide

> **Working directory:** `C:\dev\SliceForge`  
> **Target:** .NET 10  
> **Status:** Initial implementation plan  
> **Primary goal:** Build a professional, composable Vertical Slice toolkit for modern .NET applications, then layer a `dotnet new` template and an interactive CLI on top of it.

---

## 0. Product Contract

### 0.1 Working product identity

**Product:** `SliceForge`

**Positioning:**  
Opinionated building blocks and scaffolding for modern .NET applications using Vertical Slice Architecture.

**Important naming rule:**  
`SliceForge` is the toolkit name. It must **not** become part of generated application names.

Example:

```bash
sliceforge new
```

User enters:

```text
DispatchFlow
```

Generated output:

```text
DispatchFlow.slnx
src/DispatchFlow.Api/
tests/DispatchFlow.Api.Tests/
```

Not:

```text
SliceForge.DispatchFlow.Api
```

### 0.2 Product authorship

Author/credit belongs in package metadata, repository metadata, documentation, and license information.

Do not encode the author's name into the public API unless there is a strong product reason.

Example future package metadata:

```xml
<Authors>Clint Villanueva</Authors>
<Product>SliceForge</Product>
<RepositoryUrl>...</RepositoryUrl>
```

### 0.3 Core engineering principle

> **SliceForge owns plumbing. Consumer applications own business decisions.**

SliceForge may own:

- Result primitives
- Command/query abstractions
- explicit Runtime routing
- optional FluentValidation sender decoration
- Result-to-HTTP mapping
- validation ProblemDetails mapping
- integration guidance for ASP.NET Core exception handling
- Logging/tracing integration
- Diagnostics
- Project scaffolding
- Templates
- CLI generation experience

Consumer applications own:

- Entities
- Domain rules
- Use cases
- Domain-specific errors
- Database schema
- Persistence implementation
- Authentication/authorization policy
- External provider integrations
- Business workflows

### 0.4 Design philosophy

SliceForge must be:

- **Opinionated by default**
- **Composable**
- **Opt-out friendly**
- **Testable**
- **Not magical**
- **Not tied to one database**
- **Not tied to one authentication system**
- **Not tied to one application name**
- **Pleasant for both beginners and experienced .NET developers**

Runtime and optional Validation are composed explicitly:

```csharp
builder.Services.AddSliceForgeRuntime();
```

Validation is added explicitly when needed:

```csharp
builder.Services
    .AddSliceForgeValidator<CreateUserCommand, CreateUserValidator>()
    .AddSliceForgeValidation();
```

ASP.NET integration maps Core Results only. It does not compose Runtime or
Validation and does not own consumer endpoint registration or exception
middleware configuration.

---

# 1. Agent Execution Rules

These rules are mandatory for the implementation agent.

## 1.1 Work incrementally

Do not implement the entire roadmap in one pass.

Each milestone must:

1. compile,
2. pass tests,
3. remain understandable,
4. be reviewable independently,
5. avoid speculative abstractions.

## 1.2 Do not over-engineer early

Do **not** pre-create capabilities without a concrete dependency or consumer need:

```text
SliceForge.Core
SliceForge.Messaging
SliceForge.Results
SliceForge.Endpoints
SliceForge.Logging
SliceForge.Persistence
SliceForge.Authentication
...
```

Start with only the packages justified by actual dependency boundaries.

Current package set:

```text
SliceForge.Core
SliceForge.Runtime
SliceForge.Validation (optional)
SliceForge.AspNetCore
SliceForge.Observability (optional)
```

Additional packages are introduced only when a real dependency reason appears.

## 1.3 Every feature must answer four questions

Before adding a capability, document:

1. What problem does it solve?
2. Does it belong in SliceForge or in the consumer application?
3. Can the consumer opt out?
4. Can we prove its behavior with tests?

If these cannot be answered clearly, do not add the feature yet.

## 1.4 Preserve package independence

`SliceForge.Core` must not depend on ASP.NET Core.

Core should not know about:

- `WebApplication`
- `HttpContext`
- Minimal API result types
- Swagger/OpenAPI
- ASP.NET exception handlers
- authentication middleware
- HTTP status codes

`SliceForge.AspNetCore` may depend on `SliceForge.Core` and the
`Microsoft.AspNetCore.App` shared framework. It remains a class library using
`Microsoft.NET.Sdk` with an explicit framework reference.

Dependency direction:

```text
Consumer Application
   ├──► SliceForge.AspNetCore ──► SliceForge.Core
   │       └── Microsoft.AspNetCore.App framework reference
   ├──► SliceForge.Runtime ─────► SliceForge.Core
   ├──► SliceForge.Validation ──► SliceForge.Runtime
   └──► SliceForge.Observability ─► SliceForge.Runtime
           ├── Microsoft.Extensions.DependencyInjection.Abstractions
           └── Microsoft.Extensions.Logging.Abstractions
```

`SliceForge.AspNetCore` must not reference Runtime or Validation. Core must not
reference any ASP.NET Core API. Never reverse these relationships.


## 1.5 Engineering Standards — Mandatory Agent Guardrails

The agent must treat SliceForge as a reusable library product, not as a throwaway application.

These standards apply to **every milestone and every code change**.

### A. Prefer simple, explicit code

Use the simplest design that correctly expresses the current requirement.

Prefer:

```csharp
public static IServiceCollection AddSliceForgeValidator<TMessage, TValidator>(
    this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    return services;
}
```

over unnecessary factories, managers, providers, builders, wrappers, or reflection layers that do not solve a demonstrated problem.

Do not create an abstraction merely because one *might* be useful later.

Before creating an interface, ask:

> Is there currently more than one implementation, a real test seam, a public extension point, or a dependency boundary that requires it?

If not, prefer a concrete implementation.

### B. Keep public API surface intentionally small

Everything should be `internal` by default unless a consumer genuinely needs it.

Use `public` only for:

- consumer-facing abstractions,
- extension methods,
- options types,
- result/error contracts,
- documented extension points.

Do not expose implementation classes merely because tests need access to them.

If tests need internal access, prefer:

```csharp
[assembly: InternalsVisibleTo("SliceForge.Core.Tests")]
```

when justified.

Every new public type/member is a compatibility commitment and must be reviewed deliberately.

### C. Follow standard .NET naming

Use standard .NET casing and terminology.

Examples:

```text
ICommand
ICommandHandler
Result<T>
ValidationMessageSender
AddSliceForge
ToHttpResult
```

`ToHttpResult` is the approved ASP.NET integration extension. Consumer
applications map their own endpoints explicitly; SliceForge does not discover
or scan endpoints.

Avoid:

```text
CommandManager
HelperUtil
CommonService
BaseManager
MiscExtensions
Utils
ProcessorFactoryProvider
```

unless the name accurately represents a real concept.

Do not use abbreviations unless they are broadly established (`HTTP`, `API`, `URI`, etc.).

### D. Organize by responsibility, not arbitrary technical dumping grounds

Avoid folders such as:

```text
Helpers/
Utils/
Common/
Misc/
Managers/
Services/
```

unless they truly represent a bounded concept.

Prefer:

```text
Messaging/
Results/
Behaviors/
ProblemDetails/
Diagnostics/
DependencyInjection/
```

A file should have a clear reason to live where it lives.

### E. One primary responsibility per type

Do not create giant classes.

Examples:

```text
ValidationMessageSender
→ validation orchestration only

ObservabilityMessageSender
→ message-level logging/telemetry only

ResultHttpExtensions
→ expected Result failure to HTTP translation; success remains consumer-owned
```

Do not make one extension class configure:

```text
validation
OpenTelemetry
authentication
database
health checks
endpoints
Swagger
```

unless it is explicitly a high-level composition method delegating to smaller modules.

### F. Composition over inheritance

Prefer composition and small interfaces.

Avoid framework-specific base classes such as:

```csharp
public abstract class BaseCommandHandler<...>
```

unless inheritance supplies real reusable behavior that cannot be expressed cleanly through composition.

Do not require consumers to inherit from SliceForge implementation types simply to use the library.

### G. Dependency Injection rules

Use constructor injection.

Avoid:

- service locator patterns,
- static service resolution,
- storing `IServiceProvider` globally,
- calling `BuildServiceProvider()` during registration,
- resolving scoped services from the root provider,
- hidden singleton state.

Bad:

```csharp
var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<...>>();
```

Do not do this inside library registration.

Registrations must respect lifetimes deliberately.

When adding a service, document why it is:

```text
Transient
Scoped
Singleton
```

if the choice is not obvious.

### H. Async and cancellation rules

Any asynchronous API that performs I/O or delegates to asynchronous work should accept and propagate `CancellationToken`.

Prefer:

```csharp
await sender.Send(command, cancellationToken);
```

Do not silently replace it with:

```csharp
CancellationToken.None
```

Avoid blocking async code:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

unless there is an exceptional, documented reason.

Use `ConfigureAwait(false)` only when there is a concrete library-level reason; do not scatter it mechanically.

### I. Result vs exception policy

Expected application outcomes use `Result`.

Examples:

```text
validation failure
not found
conflict
forbidden business operation
```

Unexpected technical failures may use exceptions and are handled at the application boundary.

Do not:

- throw exceptions for normal validation flow,
- swallow unexpected exceptions,
- convert every exception into `Result.Failure`,
- catch exceptions only to immediately rethrow them without adding useful context.

When rethrowing:

```csharp
throw;
```

not:

```csharp
throw exception;
```

to preserve the original stack trace.

### J. Logging rules

Use structured logging.

Prefer:

```csharp
logger.LogInformation(
    "Handled {UseCase} in {ElapsedMs}ms",
    useCase,
    elapsedMs);
```

Avoid:

```csharp
logger.LogInformation($"Handled {useCase} in {elapsedMs}ms");
```

Do not globally log:

- request bodies,
- passwords,
- authorization headers,
- cookies,
- tokens,
- secrets,
- arbitrary personal data.

Avoid duplicate exception logs. One layer should own exception logging.

### K. Reflection rules

Reflection is not part of the current execution or endpoint model. Runtime
routes and consumer endpoints are registered explicitly; do not add assembly
scanning or reflection-based discovery without a separately approved feature.

When reflection is introduced:

1. keep it at startup where possible,
2. cache results if repeated work is unnecessary,
3. avoid reflection on every request,
4. test discovery behavior,
5. make failure modes explicit,
6. prefer compile-time constraints when possible.

Do not introduce reflection to solve something generics or normal registration can solve cleanly.

### L. Avoid hidden magic

A developer reading:

```csharp
builder.Services.AddSliceForgeRuntime();
```

should be able to discover exactly what it registers.

High-level convenience methods must delegate to clearly named lower-level methods.

Example conceptual structure:

```csharp
AddSliceForgeRuntime()
AddSliceForgeValidator<TMessage, TValidator>()
AddSliceForgeValidation()
```

Do not silently add unrelated features such as persistence, authentication, caching, or cloud integrations.

### M. Package dependency discipline

Before adding any NuGet dependency:

1. verify the package is actively maintained,
2. verify current license terms,
3. verify .NET 10 compatibility,
4. explain why the dependency is necessary,
5. check whether the BCL/framework already solves the problem,
6. add the version through central package management,
7. avoid bringing a large dependency for a tiny helper.

The agent must not add packages casually.

Any new external dependency must be listed in the completion report with:

```text
Package
Version
Purpose
License
Why it is justified
```

### N. Nullable reference types remain enabled

Do not use widespread null-forgiving operators:

```csharp
value!
```

to silence compiler warnings.

Fix the model instead.

Use:

```csharp
ArgumentNullException.ThrowIfNull(...)
```

at public boundaries when appropriate.

Avoid nullable states that cannot actually occur.

### O. Immutability by default

Prefer immutable contracts:

```csharp
public sealed record CreateUserCommand(...);
```

and read-only state where practical.

Do not expose mutable collections from public APIs.

Prefer:

```csharp
IReadOnlyDictionary<string, string[]>
```

over exposing mutable `Dictionary<,>` as part of public contracts unless mutation is intentional.

### P. Avoid primitive obsession in the framework only when justified

Do not create dozens of tiny wrapper types prematurely.

For example, do not immediately create:

```text
UseCaseName
ErrorCode
EndpointName
PackageName
```

unless they provide real correctness or API value.

Strong types should solve an actual problem, not merely make the architecture look sophisticated.

### Q. XML documentation on public API

Consumer-facing public members should have concise XML documentation when their behavior is not self-evident.

Especially document:

- public interfaces,
- options,
- extension methods,
- non-obvious Result behavior,
- exceptions a public method intentionally throws.

Do not add noisy documentation such as:

```csharp
/// <summary>
/// Gets the name.
/// </summary>
public string Name { get; }
```

when it adds no information.

### R. Analyzer and warning policy

Warnings are treated as errors.

Do not disable warnings globally to make CI green.

If a warning must be suppressed:

1. scope suppression as narrowly as possible,
2. explain why,
3. include the decision in the agent report.

Prefer solving the warning.

### S. Testing style

Tests should verify observable behavior and architectural invariants.

Prefer naming such as:

```text
Invalid_command_should_not_execute_handler
Valid_command_should_execute_handler_once
Internal_validator_should_be_discovered
Failure_result_should_preserve_error
```

Avoid tests that merely mirror implementation line-by-line.

For bugs, add a regression test before or alongside the fix.

Do not use random timing/sleeps to make asynchronous tests pass.

### T. Keep tests deterministic

Do not depend on:

- current wall-clock time when avoidable,
- random IDs without controlling them,
- real network services,
- machine-specific paths,
- test execution order.

Use `TimeProvider`, test doubles, or deterministic inputs when relevant.

### U. Do not optimize prematurely

Do not add caching, pooled objects, source generators, custom allocators, compiled expressions, or complex reflection caches until measurements or architecture justify them.

Correctness and clean API design come first.

### V. Comments explain why, not what

Good:

```csharp
// Logging stays outermost so validation failures are still observable.
```

Bad:

```csharp
// Add sender decoration.
services.Add...
```

Do not litter obvious code with comments.

### W. No dead or speculative code

Do not commit:

- unused options,
- placeholder interfaces,
- commented-out implementations,
- future feature stubs,
- empty abstractions "for later",
- TODOs without an issue/decision context.

If a feature is not part of the current milestone, leave it out.

### X. Keep commits milestone-focused

The agent must not perform opportunistic unrelated refactors while implementing a milestone.

If unrelated issues are discovered:

```text
Observed issue:
Recommended follow-up:
Reason not changed now:
```

Report them instead of silently expanding scope.

---

## 1.6 Architectural Decision Records

For decisions that affect the long-term public API or dependency model, create a small ADR under:

```text
docs/decisions/
```

Naming:

```text
0001-result-model.md
0002-mediatr-dependency.md
0003-validation-failure-semantics.md
```

Each ADR should contain:

```text
# Title

Status: Proposed | Accepted | Superseded

## Context

## Decision

## Consequences
```

Do not create ADRs for trivial implementation details.

Use them for decisions such as:

- why MediatR is or is not used,
- why validation returns `Result`,
- package boundaries,
- template generation strategy,
- observability dependency choices,
- public API breaking changes.

---

## 1.7 Required Agent Pre-Implementation Review

Before coding each milestone, the agent must first produce a short internal implementation plan covering:

```text
1. Files expected to change
2. Public API being introduced
3. Dependencies being added
4. Tests to be added
5. Architecture boundary impact
6. Known risks
```

Then implement the milestone.

The agent must not redesign unrelated parts of SliceForge while executing the milestone.

---

## 1.8 Required Agent Post-Implementation Review

Before declaring a milestone complete, inspect the final diff for:

```text
□ unnecessary public types
□ duplicate abstractions
□ accidental ASP.NET dependency in Core
□ unused code
□ unnecessary reflection
□ service-locator usage
□ incorrect DI lifetimes
□ swallowed exceptions
□ missing CancellationToken propagation
□ nullable suppressions
□ logging of sensitive payloads
□ dependency creep
□ duplicated package versions
□ test gaps
□ naming inconsistencies
□ formatting issues
```

Any problem discovered must either be fixed before completion or explicitly reported.

---

## 1.9 Definition of Done

A milestone is not complete merely because the code compiles.

A milestone is complete only when:

```text
✓ Requirement implemented
✓ Package boundaries preserved
✓ Public API reviewed
✓ No speculative abstractions
✓ Appropriate tests added
✓ Regression risks covered
✓ Documentation updated
✓ No new unexplained dependencies
✓ dotnet format --verify-no-changes passes
✓ Release build passes
✓ Tests pass
✓ Zero warnings
✓ Zero errors
✓ Final diff reviewed
```

For public API changes, the completion report must include a short section:

```text
Public API introduced:
Why it must be public:
Alternative considered:
```

---

# 2. Milestone 0 — Repository Foundation

## Goal

Create a clean repository that builds and tests before framework logic is added.

## 2.1 Enter the working directory

PowerShell:

```powershell
cd C:\dev\SliceForge
```

Confirm:

```powershell
Get-Location
```

Expected:

```text
C:\dev\SliceForge
```

## 2.2 Initialize Git

If the folder is not already a Git repository:

```powershell
git init
```

Create a `.gitignore` using the .NET template:

```powershell
dotnet new gitignore
```

## 2.3 Create the solution

Prefer `.slnx` for the modern .NET solution format:

```powershell
dotnet new sln -n SliceForge --format slnx
```

Expected:

```text
SliceForge.slnx
```

If the installed SDK does not support `--format slnx`, inspect:

```powershell
dotnet new sln --help
```

Use the supported equivalent rather than inventing a workaround.

## 2.4 Create repository folders

Create:

```text
src/
tests/
samples/
templates/
tools/
docs/
```

PowerShell:

```powershell
New-Item -ItemType Directory -Force src, tests, samples, templates, tools, docs
```

## 2.5 Create initial projects

Core library:

```powershell
dotnet new classlib `
  -n SliceForge.Core `
  -o src/SliceForge.Core `
  -f net10.0
```

ASP.NET Core integration library:

```powershell
dotnet new classlib `
  -n SliceForge.AspNetCore `
  -o src/SliceForge.AspNetCore `
  -f net10.0
```

Core tests:

```powershell
dotnet new xunit `
  -n SliceForge.Core.Tests `
  -o tests/SliceForge.Core.Tests `
  -f net10.0
```

ASP.NET Core tests:

```powershell
dotnet new xunit `
  -n SliceForge.AspNetCore.Tests `
  -o tests/SliceForge.AspNetCore.Tests `
  -f net10.0
```

Sample API:

```powershell
dotnet new webapi `
  -n SliceForge.Sample.Api `
  -o samples/SliceForge.Sample.Api `
  -f net10.0
```

Do not add real SliceForge behavior yet.

## 2.6 Add projects to the solution

```powershell
dotnet sln SliceForge.slnx add src/SliceForge.Core/SliceForge.Core.csproj
dotnet sln SliceForge.slnx add src/SliceForge.AspNetCore/SliceForge.AspNetCore.csproj
dotnet sln SliceForge.slnx add tests/SliceForge.Core.Tests/SliceForge.Core.Tests.csproj
dotnet sln SliceForge.slnx add tests/SliceForge.AspNetCore.Tests/SliceForge.AspNetCore.Tests.csproj
dotnet sln SliceForge.slnx add samples/SliceForge.Sample.Api/SliceForge.Sample.Api.csproj
```

## 2.7 Establish project references

ASP.NET Core integration depends on Core:

```powershell
dotnet add src/SliceForge.AspNetCore/SliceForge.AspNetCore.csproj `
  reference src/SliceForge.Core/SliceForge.Core.csproj
```

Core tests depend on Core:

```powershell
dotnet add tests/SliceForge.Core.Tests/SliceForge.Core.Tests.csproj `
  reference src/SliceForge.Core/SliceForge.Core.csproj
```

ASP.NET Core tests depend on ASP.NET Core integration:

```powershell
dotnet add tests/SliceForge.AspNetCore.Tests/SliceForge.AspNetCore.Tests.csproj `
  reference src/SliceForge.AspNetCore/SliceForge.AspNetCore.csproj
```

Sample API depends on ASP.NET Core integration:

```powershell
dotnet add samples/SliceForge.Sample.Api/SliceForge.Sample.Api.csproj `
  reference src/SliceForge.AspNetCore/SliceForge.AspNetCore.csproj
```

## 2.8 Add central repository configuration

Create:

```text
Directory.Build.props
Directory.Packages.props
```

Suggested `Directory.Build.props` baseline:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest</AnalysisLevel>
    <Deterministic>true</Deterministic>
  </PropertyGroup>
</Project>
```

Do not suppress analyzers globally just to obtain a green build.

`Directory.Packages.props` should become the single source of truth for package versions once external dependencies are introduced.

Do not add package references merely because they may be useful later.

## 2.9 Add root documentation

Create:

```text
README.md
docs/architecture.md
docs/roadmap.md
docs/decisions/
```

The README at this stage should state clearly that SliceForge is in early development.

## 2.10 Foundation verification

Run:

```powershell
dotnet restore
dotnet build SliceForge.slnx
dotnet test SliceForge.slnx
```

Required outcome:

```text
Build succeeded.
0 Warning(s)
0 Error(s)
```

Tests must pass.

Do not proceed if the repository foundation is not clean.

### Milestone 0 exit condition

Repository should resemble:

```text
SliceForge/
│
├── src/
│   ├── SliceForge.Core/
│   └── SliceForge.AspNetCore/
│
├── tests/
│   ├── SliceForge.Core.Tests/
│   └── SliceForge.AspNetCore.Tests/
│
├── samples/
│   └── SliceForge.Sample.Api/
│
├── templates/
├── tools/
├── docs/
│   ├── architecture.md
│   ├── roadmap.md
│   └── decisions/
│
├── Directory.Build.props
├── Directory.Packages.props
├── README.md
├── .gitignore
└── SliceForge.slnx
```

Suggested commit:

```text
chore: establish SliceForge repository foundation
```

---

# 3. Milestone 1 — Core Result Model

## Goal

Core result/error primitives are complete. Later capabilities must remain
outside Core and must not introduce MediatR or transport concerns into it.

Suggested structure:

```text
src/SliceForge.Core/
└── Results/
    ├── Error.cs
    ├── ErrorType.cs
    ├── Result.cs
    ├── ResultOfT.cs
    └── IValidationResult.cs
```

## 3.1 ErrorType

Keep categories generic.

Possible initial categories:

```csharp
public enum ErrorType
{
    Failure,
    Validation,
    NotFound,
    Conflict,
    Unauthorized,
    Forbidden
}
```

Do not encode HTTP status codes here. HTTP mapping belongs in `SliceForge.AspNetCore`.

## 3.2 Error

Keep `Error` immutable.

It should support:

- stable error code,
- user-safe description,
- error type,
- optional structured validation details where justified.

Do not add application-specific errors.

Wrong:

```csharp
DeliveryJobErrors.DuplicateReference
```

Correct:

Consumer application defines that.

## 3.3 Result and Result<T>

Required invariants:

```text
Success → no error
Failure → must have error
Result<T>.Success → value is available
Result<T>.Failure → error is available
```

Do not add HTTP concerns.

## 3.4 Test all invariants

Examples:

```text
Success result reports IsSuccess = true
Failure result reports IsSuccess = false
Failure cannot exist without an Error
Result<T> preserves its value
Validation failure preserves field-level errors
```

### Milestone 1 exit condition

`SliceForge.Core` contains a stable, thoroughly tested result abstraction and still has no ASP.NET dependency.

Suggested commit:

```text
feat(core): introduce result and error primitives
```

---

# 4. Milestone 2 — Messaging Abstractions

## Goal

Introduce SliceForge's command/query API.

Suggested structure:

```text
src/SliceForge.Core/
└── Messaging/
    ├── IBaseCommand.cs
    ├── ICommand.cs
    ├── ICommandHandler.cs
    ├── IQuery.cs
    └── IQueryHandler.cs
```

Target concepts:

```csharp
IBaseCommand
ICommand
ICommand<TResponse>
ICommandHandler<TCommand>
ICommandHandler<TCommand, TResponse>
IQuery<TResponse>
IQueryHandler<TQuery, TResponse>
```

The application-facing contract should preserve the Result pattern.

Conceptually:

```text
ICommand<Guid>
      ↓
Result<Guid>
```

not:

```text
ICommand<Result<Guid>>
```

for consumer-facing declaration syntax.

## Dependency decision

Milestone 2 owns the SliceForge messaging contracts directly. The Core project
must not reference MediatR or another mediator library, and these interfaces
must not inherit from third-party request or handler contracts.

MediatR 14.2.0 is technically compatible with .NET 10, but its current
Reciprocal Public License 1.5/commercial licensing model and the resulting
public-API coupling make it unsuitable as a mandatory SliceForge.Core
dependency without a separate product and licensing decision.

MediatR may be evaluated later as an optional adapter outside Core. Any such
adapter is a separate milestone and must not introduce MediatR into these
public contracts. This completed Milestone 2 gate did not add a dispatcher,
sender, DI registration, reflection, or assembly scanning.

### Milestone 2 tests

At minimum test the SliceForge-owned compile-time and runtime expectations
around:

- command and query marker relationships,
- handler response signatures,
- success and failure result pass-through,
- nullable response payloads,
- cancellation-token acceptance,
- approved variance and generic constraints.

Runtime handler resolution and exactly-one-handler integration tests are
deferred until a future dispatch/adapter milestone.

Suggested commit:

```text
feat(core): introduce messaging abstractions
```

---

# 5. Milestone 4 — Validation Decoration

## Goal

Keep validation outside Core and Runtime by decorating the scoped
`IMessageSender` from `SliceForge.Validation`.

```text
SliceForge.Validation
    → SliceForge.Runtime
        → SliceForge.Core
```

FluentValidation is optional and is referenced only by the Validation package.
Validators are registered explicitly with
`AddSliceForgeValidator<TMessage, TValidator>()`; assembly scanning and
base/interface discovery are not used.

## 5.1 Required behavior semantics

Validation route execution returns an internal `Error?`. The sender decorator
constructs the result shape required by the sender overload:

```text
ICommand    → Result.Failure(error)
ICommand<T> → Result<T>.Failure(error)
IQuery<T>   → Result<T>.Failure(error)
```

Invalid requests return `ErrorType.Validation` with code
`Validation.Failed`, skip the inner sender, and therefore skip the handler.
Validator exceptions and cancellation propagate as exceptions. They are never
converted into expected Result failures.

## 5.2 Important regression invariants

Tests must guarantee:

```text
No SliceForge validator route
→ inner sender executes exactly once

Valid request
→ inner sender executes exactly once

Invalid request
→ inner sender executes zero times

Multiple validators
→ sequential registration order and aggregated failures

Duplicate property/message pairs
→ removed while first-seen order is preserved

Object-level failure
→ empty property name preserved

Cancellation or validator exception
→ propagated without invoking the handler
```

Validation metadata is cached immutably. Validator instances are resolved from
the current DI scope at execution time and are transient by default.

## 5.3 Sender decoration ordering

Milestone 7 adds observability through the same explicit sender-decoration mechanism:

```text
ObservabilityMessageSender
      ↓
ValidationMessageSender
      ↓
DefaultMessageSender
      ↓
Handler
```

This is intentionally not a public pipeline-stage abstraction or a renamed
MediatR `IPipelineBehavior`.

Suggested commit:

```text
feat(validation): add sender validation decorator
```

---

# 6. Milestone 5 — ASP.NET Core Integration (completed)

## Goal

Map SliceForge Results to ASP.NET Core HTTP results without contaminating Core.
SliceForge owns expected Result failure mapping; consumer applications own
successful HTTP semantics, endpoint mapping, authentication configuration,
and exception middleware configuration.

The project remains a class library using `Microsoft.NET.Sdk` and references
`SliceForge.Core` plus the `Microsoft.AspNetCore.App` shared framework. It must
not reference Runtime, Validation, FluentValidation, or MediatR.

The public mapping surface is limited to the two success-callback overloads:

```csharp
namespace SliceForge.AspNetCore.Results;

public static class ResultHttpExtensions
{
    public static IResult ToHttpResult(
        this Result result,
        Func<IResult> onSuccess);

    public static IResult ToHttpResult<TResponse>(
        this Result<TResponse> result,
        Func<TResponse, IResult> onSuccess);
}
```

Both overloads reject null arguments. A successful result invokes its callback
exactly once and returns that exact `IResult`; a null callback result throws
`InvalidOperationException`. Callback exceptions propagate unchanged. The
typed overload passes the exact successful value, including null when
permitted by `TResponse`.

## 6.1 Expected Result failure mapping

| `ErrorType` | HTTP response |
| --- | --- |
| `Failure` | 400 ProblemDetails |
| `Validation` | 400 `HttpValidationProblemDetails` |
| `NotFound` | 404 ProblemDetails |
| `Conflict` | 409 ProblemDetails |
| `Unauthorized` | ASP.NET Core `Results.Challenge()` |
| `Forbidden` | ASP.NET Core `Results.Forbid()` |

For ordinary ProblemDetails, expose the mapped `status`, `Error.Description`
as `detail`, and `Error.Code` as the `code` extension. Do not serialize the
Core `Error`, `Error.Type`, exception details, or implementation internals.

Validation mapping uses the Core validation groups as the ASP.NET
`Errors` dictionary (`string[]` values), preserves empty property keys for
object-level failures, and removes duplicate messages within each property
while preserving first-seen order. The Core collections are never mutated.
The response retains `Error.Description` as `detail` and `Error.Code` as the
`code` extension.

Unauthorized and Forbidden are authentication results, not ProblemDetails.
SliceForge calls `Results.Challenge()` and `Results.Forbid()` without choosing
an authentication scheme or writing authentication headers. The consuming
application configures authentication. In particular, it must configure an
applicable challenge for a 401 response.

## 6.2 Unexpected exceptions and endpoint ownership

HTTP 500 is reserved for unexpected exceptions. They remain exceptions and
must not be converted to `Result.Failure`. Consumer applications configure
ASP.NET Core directly:

```csharp
builder.Services.AddProblemDetails();
app.UseExceptionHandler();
```

SliceForge does not provide `AddSliceForgeAspNetCore()`, a custom
`IExceptionHandler`, or a duplicate exception logger. The framework exception
middleware owns unexpected-exception handling and logging.

Consumer endpoints explicitly map their routes, inject `IMessageSender` if
needed, and choose success responses through `ToHttpResult`. Endpoint
abstractions, endpoint discovery, assembly scanning, and Runtime integration
are deferred; the Sample API must demonstrate a real need before any such
capability is reconsidered.

ASP.NET tests cover callback behavior, all Result mappings, validation
grouping/deduplication/object-level errors, and execution of the configured
authentication challenge/forbid. Framework exception middleware is configured
by consumers and is not reimplemented by SliceForge.

Suggested commit:

```text
feat(aspnetcore): add result HTTP mapping
```

---

# 7. Milestone 6 — Sample API

## Goal

Use SliceForge as an external consumer would.

Completed feature:

```text
samples/SliceForge.Sample.Api/
├── Features/Todos/Create/
│   ├── CreateTodoCommand.cs
│   ├── CreateTodoValidator.cs
│   ├── CreateTodoHandler.cs
│   └── CreateTodoEndpoint.cs
├── Features/Todos/GetById/
│   ├── GetTodoQuery.cs
│   ├── GetTodoHandler.cs
│   └── GetTodoEndpoint.cs
├── Features/Todos/Complete/
│   ├── CompleteTodoCommand.cs
│   ├── CompleteTodoHandler.cs
│   └── CompleteTodoEndpoint.cs
├── Features/Todos/TodoResponse.cs
└── Infrastructure/TodoStore.cs
```

The sample is a compatibility and usability test. It demonstrates `ICommand<TResponse>`, `ICommand`, and `IQuery<TResponse>`, explicit handler and validator registration, exact Runtime routing, optional Validation decoration, and consumer-owned HTTP success responses. `TodoStore` is a concrete lock-protected in-memory dictionary; persistence, repositories, and UnitOfWork are out of scope.

The sample API directly references Core, Runtime, Validation, and AspNetCore because its source consumes all four packages. Its HTTP integration test project references the API and uses `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 as a centrally managed, test-only dependency.

## Consumer registration and explicit routes

Runtime is registered before Validation; handlers and validators are explicit and registered before building the service provider:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSingleton<TodoStore>();
builder.Services.AddSliceForgeCommandHandler<CreateTodoCommand, Guid, CreateTodoHandler>();
builder.Services.AddSliceForgeCommandHandler<CompleteTodoCommand, CompleteTodoHandler>();
builder.Services.AddSliceForgeQueryHandler<GetTodoQuery, TodoResponse, GetTodoHandler>();
builder.Services.AddSliceForgeValidator<CreateTodoCommand, CreateTodoValidator>();
builder.Services.AddSliceForgeValidation();

var app = builder.Build();

app.UseExceptionHandler();
CreateTodoEndpoint.Map(app);
GetTodoEndpoint.Map(app);
CompleteTodoEndpoint.Map(app);

app.Run();
```

Routes are mapped explicitly; there is no `IEndpoint`, endpoint discovery, assembly scanning, controller, or `MapSliceForgeEndpoints` API. Every endpoint injects `IMessageSender`, passes its request cancellation token unchanged, and maps the completed result using `ToHttpResult`.

The routes are `POST /todos` (`ICommand<Guid>` → 201 Created), `GET /todos/{id:guid}` (`IQuery<TodoResponse>` → 200 OK or NotFound), and `PUT /todos/{id:guid}/complete` (`ICommand` → 204 No Content, NotFound, or Conflict). Invalid create input is returned by SliceForge.Validation; missing todos use `ErrorType.NotFound`, and already-completed todos use `ErrorType.Conflict`. These expected outcomes are Results, not exceptions.

### Milestone 6 exit condition

Completed: eight HTTP integration tests use a fresh `WebApplicationFactory<Program>` per test and cover valid/invalid POST, POST then GET, missing GET, successful completion, repeated completion, concurrent completion, and missing completion. The tests use no real ports, sleeps, external resources, shared mutable test state, or order-dependent setup.

Suggested commit:

```text
feat(sample): add end-to-end SliceForge consumer API
```

---

# 8. Milestone 7 — Logging & Observability (completed)

`SliceForge.Observability` is an optional package over Runtime. It decorates
the scoped `IMessageSender`; Runtime and Core remain free of observability
dependencies. The package references Runtime,
`Microsoft.Extensions.DependencyInjection.Abstractions`, and
`Microsoft.Extensions.Logging.Abstractions`. It does not reference Validation,
ASP.NET Core, OpenTelemetry, MediatR, or Scrutor.

The public surface is limited to `SliceForgeInstrumentation` constants for the
activity source, meter, and logger category names, plus
`AddSliceForgeObservability(IServiceCollection)`. There is no options object.

## 8.1 Sender decoration and registration order

Register Runtime first, optional Validation next, and Observability last:

```text
ObservabilityMessageSender
    ↓
ValidationMessageSender (when enabled)
    ↓
DefaultMessageSender
    ↓
Handler
```

Observability therefore sees successful messages, expected Result failures,
validation failures, cancellation, and unexpected exceptions. It returns the
same Result object, preserves the message and cancellation token, and invokes
the inner sender exactly once. Registration requires exactly one supported
scoped, unkeyed sender and rejects duplicate enablement. A private keyed DI
registration preserves the captured descriptor's lifetime and disposal
tracking; consumers must not call Validation after Observability.

This is sender decoration, not a public pipeline-stage abstraction or a renamed
MediatR behavior. No Scrutor dependency is used.

## 8.2 Activities

The process-wide `ActivitySource` name is
`SliceForgeInstrumentation.ActivitySourceName`, whose value is
`SliceForge.Observability`. Command and query activity names are
`sliceforge.command` and `sliceforge.query`; both use `ActivityKind.Internal`
and naturally parent from `Activity.Current`.

Tags are `sliceforge.message.kind`, `sliceforge.message.type`,
`sliceforge.outcome`, and `sliceforge.error.type` for returned failures only.
Outcomes are `success`, `failure`, `validation_failure`, `cancelled`, and
`exception`. Success sets status `Ok`; expected failures, validation failures,
and cancellation leave status `Unset`; unexpected exceptions set `Error`.
Never record payloads, request or validation values/messages, error code or
description, credentials, or exception details.

## 8.3 Metrics

The process-wide `Meter` name is `SliceForgeInstrumentation.MeterName`, whose
value is `SliceForge.Observability`. The only instruments are:

| Instrument | Type | Unit |
| --- | --- | --- |
| `sliceforge.messaging.executions` | `Counter<long>` | `{execution}` |
| `sliceforge.messaging.duration` | `Histogram<double>` | `s` |

Record once per send, including cancellation and exceptions. Duration includes
the complete inner sender call, including Validation when it is inside the
decorator. Metric tags are limited to `sliceforge.message.kind` and
`sliceforge.outcome`; message type, `ErrorType`, error code, and other
application-defined strings are not metric dimensions.

## 8.4 Structured logging and exception ownership

The stable logger category is `SliceForge.Observability`. Fixed events are:

| Event ID | Name | Level |
| --- | --- | --- |
| 1000 | `MessageSucceeded` | Debug |
| 1001 | `MessageFailed` | Information |
| 1002 | `MessageValidationFailed` | Information |
| 1003 | `MessageCancelled` | Debug |

Structured fields are limited to `MessageKind`, `MessageType`, `Outcome`, and
`ErrorType` for returned failures. Do not log payloads, values, descriptions,
codes, validation messages, or exception details. Unexpected exceptions are
marked in activities/metrics and rethrown unchanged; this package does not
log them, leaving the single exception log to the outer exception owner.
Cancellation remains `OperationCanceledException` and is rethrown unchanged.
Telemetry emission is best-effort and cannot replace results or mask the
sender's cancellation/exception.

## 8.5 OpenTelemetry and service identity

SliceForge uses .NET `ActivitySource`, `Meter`, and Logging abstractions and
has no OpenTelemetry dependency. Consumers may opt in externally:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
        tracing.AddSource(SliceForgeInstrumentation.ActivitySourceName))
    .WithMetrics(metrics =>
        metrics.AddMeter(SliceForgeInstrumentation.MeterName));
```

The consumer owns `service.name`, `ResourceBuilder`, exporters, sampling, and
the telemetry backend. Instrumentation source/meter names identify SliceForge,
not the consuming application. No observability options are added until a
real configurable choice is demonstrated.

Suggested commit:

```text
feat(observability): add sender execution instrumentation
```

---

# 9. Milestone 8 — Configuration Audit (completed; no API added)

The Sample API demonstrates no setting that requires a shared SliceForge
configuration model. Explicit feature registration is the composition and
configuration mechanism:

```csharp
builder.Services.AddSliceForgeRuntime();
builder.Services.AddSliceForgeValidation();
builder.Services.AddSliceForgeObservability();
```

Register Runtime first, then handlers and validators, then sender decorators in
the intended order. Do not add `SliceForgeOptions`, `AddSliceForge`, feature
booleans, or placeholder Validation/Observability options without a demonstrated
consumer-controlled setting. Database and authentication configuration remain
consumer-owned. Endpoint discovery is not provided and has no options.

This audit is complete without production configuration API.

---

# 10. Milestone 8 — Package Validation (completed and published as preview packages)

The five public runtime packages target `net10.0` and share the current coordinated preview version
`0.1.0-preview.3`: `SliceForge.Core`, `SliceForge.Runtime`,
`SliceForge.Validation`, `SliceForge.AspNetCore`, and
`SliceForge.Observability`. Shared package metadata is centralized; package
identity, description, and tags remain in each packable project. The packages
use Apache-2.0 metadata and include the root README. Publishing is handled by the dedicated trusted-publishing release workflow after the full validation gate succeeds.

Generate packages and symbols locally:

```powershell
dotnet pack SliceForge.slnx -c Release -o artifacts/packages
pwsh -File scripts/validate-packages.ps1
```

The validator inspects package contents and nuspec dependency constraints,
then copies `eng/package-validation` to an isolated consumer under
`artifacts/package-validation`. That consumer has its own build properties,
central package versions, NuGet sources, and package cache; it references all
five SliceForge packages and has no project or source-tree references. It uses
`Microsoft.NET.Sdk` to verify the AspNetCore package's transitive shared
framework reference, along with Runtime dispatch, Validation, and outer
Observability behavior through the generated packages.

Package validation is complete only when the full repository gates pass, all
five `.nupkg` and `.snupkg` files pass content and metadata inspection, and the
isolated consumer restores, builds, and runs using the local feed plus
nuget.org. The GitHub Actions workflow performs those checks and never
publishes packages.

Inspect generated nuspec versions rather than assuming Central Package
Management creates exact dependency pins. The current pack output uses bare
minimum versions for package dependencies; in particular, SliceForge sibling
dependencies have no upper bound on pre-1.0 releases. Record this compatibility
risk and request an owner decision before introducing custom dependency-range
rewriting; do not silently change pack output.

---

# 11. Milestone 9 — `dotnet new` Template (completed as a preview)

The content-only NuGet package is `SliceForge.Templates` at the synchronized
`0.1.0-preview.3` version. It targets the .NET 10 template ecosystem, declares
`PackageType=Template`, and contains no SliceForge runtime dependency or
compiled assemblies.

Scriptable use:

```powershell
dotnet new install SliceForge.Templates@0.1.0-preview.3
dotnet new sliceforge-api -n DispatchFlow
```

Generated application identity comes from the requested name, not SliceForge
or author attribution:

```text
DispatchFlow/
├── Directory.Build.props
├── Directory.Packages.props
├── DispatchFlow.slnx
├── src/DispatchFlow.Api/
└── tests/DispatchFlow.Api.Tests/
```

The generated API project uses package references to the five released
SliceForge libraries. Their synchronized versions and test-only dependencies
are centrally managed in the generated `Directory.Packages.props`. It includes
one small, removable `Features/Examples/GetExample` slice. The endpoint is
mapped explicitly and shows the consumer-owned flow from `IMessageSender`
through `Result<T>` to `ToHttpResult`; no scanning, reflection, database,
repository layer, or generated Todos domain is included. Runtime, Validation,
and Observability are registered explicitly in that order of composition
(Runtime first; Validation before the outer Observability decorator), and the
application configures `AddProblemDetails()` and `UseExceptionHandler()`.

Package validation inspects the template manifest/content and rejects compiled
SliceForge assemblies. It installs the local package with an isolated
`DOTNET_CLI_HOME`, generates `DispatchFlow`, verifies generated identities and
absence of repository project references, then restores from the local package
feed plus nuget.org, builds, and runs the generated HTTP integration test. CI
runs this flow on Ubuntu without modifying a developer's global template hive
or publishing the package.

---

# 12. Milestone 10 — SliceForge.Cli

**Status: completed as a preview global tool.** The CLI is an orchestration and
user-experience layer only. `SliceForge.Templates` remains the sole source of
generated application architecture; the CLI delegates to `dotnet new` rather
than generating files itself. The directly scriptable template workflow remains
available without the CLI.

Package identity is `SliceForge.Tool`, title `SliceForge CLI`; the implementation project and assembly remain `SliceForge.Cli`. The synchronized
version is `0.1.0-preview.3`, target `net10.0`, and tool command `sliceforge`.
`System.CommandLine` 2.0.12 is centrally versioned. The tool has no dependency
on SliceForge runtime packages.

Supported commands:

```text
sliceforge                 compact branded root screen
sliceforge --help          standard command-line help
sliceforge --version       exact package version and newline
sliceforge version         exact package version and newline
sliceforge new [-n NAME] [-o DIRECTORY]
sliceforge doctor          local SDK and template checks only
```

The root screen is restrained, has no color or animation, and falls back to
ASCII when Unicode output is unavailable. `new` delegates to
`dotnet new sliceforge-api` using argument-list process construction. It prompts
for a name only when input is interactive; redirected input without a name
returns usage. If the template is absent, interactive use asks before running
`dotnet new install SliceForge.Templates@<tool-version>` and retries generation;
redirected execution never prompts or installs automatically. Requested output
directories and SDK exit codes are forwarded, and cancellation stops the child
process and returns the conventional interrupt exit code.

`doctor` uses local SDK-list and installed-template-list commands only. It does
not access package feeds, install templates, restore projects, or inspect
consumer source. Expanded project diagnostics remain Milestone 11 work.

Package validation inspects the `DotnetTool` package metadata and payload,
checks its System.CommandLine dependency and absence of SliceForge runtime
dependencies, installs the tool into an isolated tool path and CLI home, and
executes root/help/version/doctor/new. CLI-driven generation is restored from
the local package feed and nuget.org, then built and tested. CI repeats these
checks and never publishes packages.

---

# 13. Milestone 11 — SliceForge Doctor / Diagnostics

This is a later differentiating feature.

Future command:

```bash
sliceforge doctor
```

Potential checks:

```text
✓ SDK supported
✓ SliceForge packages compatible
✓ command routes explicitly registered
✓ query routes explicitly registered
✓ handler routes explicitly registered
✓ SliceForge validators explicitly registered

✗ command has no handler
✗ message has multiple handlers
⚠ validation enabled without SliceForge validators
⚠ optional feature configured but unused
```

Endpoint discovery is not a SliceForge diagnostic because consumer endpoints
are mapped explicitly and are not registered through SliceForge scanning.

Startup diagnostics may also become available programmatically.

Do not build this before the basic package and template are stable.

---

# 14. Milestone 12 — Real-World Adoption

Use a real project such as DispatchFlow as a consumer.

Migration should be incremental.

Potential package dependencies to replace application-owned copies:

```text
Result.cs
Error.cs
ICommand.cs
IQuery.cs
ICommandHandler.cs
IQueryHandler.cs
common Result → HTTP mapping
```

The sender decorators are package implementation details and should be consumed
through their registration extensions, not copied into the application.

Application keeps:

```text
DeliveryJob
DeliveryJobErrors
workers
database model
idempotency rules
provider implementations
business workflows
```

## Validation rule

If integrating SliceForge makes the real project noticeably harder to understand, first assume the abstraction may be wrong.

Do not force consumer applications to match the framework merely to protect framework design.

---

# 15. Explicitly Out of Scope for Early Versions

Do not add these to the first SliceForge implementation:

- JWT authentication framework
- ASP.NET Identity abstraction
- OAuth provider framework
- database migrations framework
- PostgreSQL-specific persistence base
- SQL Server-specific persistence base
- Redis abstraction
- Kafka/RabbitMQ abstraction
- repository pattern framework
- generic unit-of-work framework
- event sourcing
- Saga framework
- UI/frontend generation
- deployment/cloud provisioning
- automatic business-layer generation

They may be revisited later if real consumer projects repeatedly demonstrate a need.

---

# 16. Public API Naming Guidelines

Prefer conventional .NET names.

Good:

```csharp
AddSliceForgeRuntime()
AddSliceForgeValidator<TMessage, TValidator>()
AddSliceForgeValidation()
ToHttpResult()
```

Avoid excessive branding:

```csharp
AddSliceForgeSuperPipelineMagic()
UseSliceForgeEverything()
AddUltimateDefaults()
```

Avoid generic names that can collide:

```csharp
AddDefaults()
AddCore()
MapEndpoints()
```

Public APIs should make ownership obvious without becoming noisy.

---

# 17. Namespace Guidelines

Examples:

```text
SliceForge
SliceForge.Messaging
SliceForge.Results
SliceForge.Observability
SliceForge.DependencyInjection

SliceForge.AspNetCore
SliceForge.AspNetCore.Results
```

Consumer application:

```text
DispatchFlow
DispatchFlow.Features
DispatchFlow.Domain
DispatchFlow.Infrastructure
```

Do not require consumer namespaces to inherit SliceForge naming.

---

# 18. Quality Gates for Every Milestone

Before marking a milestone complete:

```powershell
dotnet format --verify-no-changes
dotnet build SliceForge.slnx -c Release
dotnet test SliceForge.slnx -c Release
```

Required:

```text
0 warnings
0 errors
all tests passing
```

Also inspect:

- public API naming,
- dependency direction,
- nullable warnings,
- test coverage of new behavior,
- documentation changes,
- package boundaries,
- accidental framework coupling.

Do not suppress warnings merely to pass the gate.

---

# 19. Suggested Commit Sequence

Use small commits.

Example:

```text
chore: establish SliceForge repository foundation

feat(core): introduce error and result primitives

feat(core): introduce messaging abstractions

feat(validation): add sender validation decorator

feat(aspnetcore): add result HTTP mapping

feat(sample): add vertical slice sample API

feat(observability): add sender execution instrumentation

feat(packaging): configure preview NuGet packages

feat(template): add sliceforge-api project template

feat(cli): add interactive project wizard

feat(diagnostics): add SliceForge doctor checks
```

Avoid commits such as:

```text
update
fix stuff
changes
wip
```

for meaningful project history.

---

# 20. First Agent Session — Historical Foundation Scope

For the first implementation session, the agent should **only perform Milestone 0**.

This section records the original foundation-only session. It is historical;
the current milestone order and package boundaries below are authoritative.

## Agent task

> Establish the SliceForge repository foundation in `C:\dev\SliceForge` using .NET 10. Create the `.slnx` solution, `SliceForge.Core`, `SliceForge.AspNetCore`, their xUnit test projects, and `SliceForge.Sample.Api`. Create the root folder structure and dependency references described in this plan. Add clean repository-wide build settings using `Directory.Build.props` and an initial `Directory.Packages.props`. Add concise `README.md`, `docs/architecture.md`, and `docs/roadmap.md`. Do not introduce framework functionality yet. Finish only when restore, format verification, Release build, and tests succeed with zero warnings and zero errors. Report every file created, every project reference, verification results, and any deviation from this plan.

## Agent completion report

At completion, provide:

```text
1. Files/folders created
2. Projects created
3. Project references
4. Repository-wide build configuration
5. Build result
6. Test result
7. Format result
8. Any SDK/template compatibility issue encountered
9. Git diff summary
10. Recommended next step
```

Do not begin Milestone 1 without explicit approval.

---

# 21. Historical Foundation Sequence

The original post-foundation sequence was:

**Milestone 1:** implement only `Error`, `ErrorType`, `Result`, `Result<T>`, and their tests.

MediatR was intentionally excluded from the original Core foundation.

This keeps the progression:

```text
Repository
    ↓
Result model
    ↓
Messaging
    ↓
Runtime
    ↓
Validation decoration
    ↓
ASP.NET integration
    ↓
Sample consumer
    ↓
Observability
    ↓
NuGet packaging
    ↓
dotnet new template
    ↓
Interactive CLI
    ↓
Diagnostics
    ↓
Real-world adoption
```

That is the intended SliceForge implementation path.

## Current milestone status and next step

Completed milestones:

- Milestone 0: repository foundation
- Milestone 1: Core result and error model
- Milestone 2: SliceForge-owned messaging contracts
- Milestone 3: Runtime execution with explicit handler registration
- Milestone 4: optional Validation sender decoration
- Milestone 5: ASP.NET Core Result mapping
- Milestone 6: end-to-end Todos Sample API
- Milestone 7: optional Logging & Observability sender decoration
- Milestone 8: configuration audit and package validation
- Milestone 9: content-only `SliceForge.Templates` package and isolated generated-app validation
- Milestone 10: `SliceForge.Tool` preview global-tool package (implemented by `SliceForge.Cli`) and isolated tool/template validation

Runtime is separate from Core and uses exact concrete-message routing. It does
not use MediatR, assembly scanning, polymorphic discovery, or a public
pipeline abstraction. MediatR remains only a possible future optional adapter
requiring separate product and licensing approval.

The earlier illustrative CLI preset that coupled the architecture to a
mediator is superseded. Future presets must describe `Vertical Slice` and may
offer an optional mediator adapter only after separate product and licensing approval.
The Runtime milestone is the execution foundation. Validation decorates its
sender and must not assume a pre-existing MediatR pipeline or reintroduce
mediator types into Core. Historical references above preserve the original
decision trail; they are not implementation instructions.

Logging & Observability is complete as an optional Runtime sender decorator;
it has no OpenTelemetry package dependency and does not own `service.name`.
The Sample API demonstrates
the consumer-owned HTTP flow with explicit routes, `IMessageSender`, and
`ToHttpResult`; the application configures `AddProblemDetails()` and
`UseExceptionHandler()` directly. Endpoint discovery remains deferred, and
`SliceForge.AspNetCore` remains independent of Runtime and Validation.

The configuration audit found no demonstrated options need and is complete
without adding a configuration API. Package validation inspects the five
synchronized libraries, the content-only template package, and the CLI tool.
It consumes library packages in an isolated package-only smoke application,
installs the template and CLI in isolated locations, executes the CLI commands,
and builds/tests applications generated both directly and through the CLI. CI
repeats those checks without publishing. Milestones 9 and 10 are complete as
preview packages; Milestone 11 is the next step for expanded diagnostics.
Keep the CLI independent from runtime libraries and keep generated architecture
owned by the directly usable template.
