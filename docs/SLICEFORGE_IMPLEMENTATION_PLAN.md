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
- Pipeline behaviors
- FluentValidation registration
- Endpoint discovery
- HTTP result mapping
- ProblemDetails conventions
- Exception handling infrastructure
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

The desired API should support both:

```csharp
builder.Services.AddSliceForge<ApplicationAssembly>();
```

and advanced composition such as:

```csharp
builder.Services
    .AddSliceForgeCore<ApplicationAssembly>()
    .AddSliceForgeValidation()
    .AddSliceForgeEndpoints();
```

Do not implement both immediately. Establish the internal boundaries first.

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

Do **not** begin by creating:

```text
SliceForge.Core
SliceForge.Messaging
SliceForge.Results
SliceForge.Validation
SliceForge.Endpoints
SliceForge.OpenTelemetry
SliceForge.Logging
SliceForge.Persistence
SliceForge.Authentication
...
```

Start with only the packages justified by actual dependency boundaries.

Initial package set:

```text
SliceForge.Core
SliceForge.AspNetCore
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

`SliceForge.AspNetCore` may depend on `SliceForge.Core`.

Dependency direction:

```text
Consumer Application
        │
        ├─────────────► SliceForge.AspNetCore
        │                       │
        │                       ▼
        └────────────────► SliceForge.Core
```

Never reverse this relationship.


## 1.5 Engineering Standards — Mandatory Agent Guardrails

The agent must treat SliceForge as a reusable library product, not as a throwaway application.

These standards apply to **every milestone and every code change**.

### A. Prefer simple, explicit code

Use the simplest design that correctly expresses the current requirement.

Prefer:

```csharp
public static IServiceCollection AddSliceForgeCore<TAssemblyMarker>(
    this IServiceCollection services)
{
    ArgumentNullException.ThrowIfNull(services);

    Assembly assembly = typeof(TAssemblyMarker).Assembly;

    // registration

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
ValidationBehavior
SliceForgeOptions
AddSliceForge
MapSliceForgeEndpoints
```

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
Endpoints/
ProblemDetails/
Diagnostics/
DependencyInjection/
```

A file should have a clear reason to live where it lives.

### E. One primary responsibility per type

Do not create giant classes.

Examples:

```text
ValidationBehavior
→ validation orchestration only

LoggingBehavior
→ message-level logging/telemetry only

EndpointExtensions
→ endpoint registration/mapping only

ResultHttpExtensions
→ Result → HTTP translation only
```

Do not make one extension class configure:

```text
MediatR
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

Reflection is allowed only when it provides clear framework value, such as endpoint or handler discovery.

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
builder.Services.AddSliceForge<ApplicationAssembly>();
```

should be able to discover exactly what it registers.

High-level convenience methods must delegate to clearly named lower-level methods.

Example conceptual structure:

```csharp
AddSliceForge<T>()
    ↓
AddSliceForgeCore<T>()
AddSliceForgeValidation()
AddSliceForgeEndpoints<T>()
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
ServiceName
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
// Add logging behavior.
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

Implement the result/error primitives before introducing MediatR, HTTP, logging, or validation.

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

## Dependency review

Before adding MediatR:

- confirm the current stable package version,
- confirm its current license,
- document the dependency decision,
- centralize the package version,
- do not duplicate handler scanning.

Do not blindly copy old dependency assumptions.

### Milestone 2 tests

At minimum test compile-time and runtime expectations around:

- commands returning `Result`,
- commands returning `Result<T>`,
- queries returning `Result<T>`,
- handler resolution,
- exactly one handler per message in the sample architecture.

Suggested commit:

```text
feat(core): introduce messaging abstractions
```

---

# 5. Milestone 3 — Validation Pipeline

## Goal

Move reusable FluentValidation pipeline behavior into SliceForge.

Suggested structure:

```text
src/SliceForge.Core/
├── Behaviors/
│   └── ValidationBehavior.cs
└── DependencyInjection/
    └── ServiceCollectionExtensions.cs
```

## 5.1 Required behavior semantics

Validation is an expected application failure.

Therefore:

```text
Invalid request
      ↓
ValidationBehavior
      ↓
failed Result
```

Not:

```text
ValidationException
```

unless the architecture is deliberately changed and documented.

## 5.2 Important regression invariants

Tests must guarantee:

```text
No validator
→ handler executes exactly once

Valid request
→ handler executes exactly once

Invalid request
→ handler executes zero times

Multiple validators
→ all relevant failures are aggregated

Duplicate error messages
→ no accidental duplication

Cancellation
→ propagated correctly
```

If internal validators are supported, explicitly test discovery of internal types.

## 5.3 Pipeline ordering

Initial intended ordering once logging is added:

```text
LoggingBehavior
      ↓
ValidationBehavior
      ↓
Handler
```

Logging should eventually remain outside validation so rejected requests can still be observed.

Suggested commit:

```text
feat(core): add validation pipeline behavior
```

---

# 6. Milestone 4 — ASP.NET Core Integration

## Goal

Create HTTP-specific functionality without contaminating Core.

Suggested structure:

```text
src/SliceForge.AspNetCore/
│
├── Endpoints/
│   ├── IEndpoint.cs
│   └── EndpointExtensions.cs
│
├── Results/
│   └── ResultHttpExtensions.cs
│
├── ProblemDetails/
│   └── ProblemDetailsExtensions.cs
│
├── Exceptions/
│   └── GlobalExceptionHandler.cs
│
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs
│
└── Options/
```

## 6.1 Endpoint abstraction

Target:

```csharp
public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder app);
}
```

Provide:

```csharp
services.AddSliceForgeEndpoints<TAssemblyMarker>();
```

and:

```csharp
app.MapSliceForgeEndpoints();
```

Do not silently scan arbitrary assemblies.

Assembly selection should be explicit or have a documented default.

## 6.2 Result → HTTP mapping

HTTP mapping lives here.

Example conceptual mapping:

```text
Validation   → 400
Unauthorized → 401
Forbidden    → 403
NotFound     → 404
Conflict     → 409
Failure      → appropriate documented fallback
```

Do not make domain/core types depend on status codes.

## 6.3 ProblemDetails

Provide one consistent API error shape.

Include trace correlation later when observability exists.

## 6.4 Exception handling

Unexpected failures:

```text
Unhandled Exception
      ↓
Global Exception Handler
      ↓
ProblemDetails
```

Expected failures:

```text
Result.Failure
      ↓
Result HTTP mapping
```

Do not mix these paths.

Suggested commit:

```text
feat(aspnetcore): add endpoint discovery and result mapping
```

---

# 7. Milestone 5 — Sample API

## Goal

Use SliceForge as an external consumer would.

Suggested feature:

```text
samples/SliceForge.Sample.Api/
└── Features/
    └── Users/
        ├── Create/
        │   ├── Command.cs
        │   ├── Validator.cs
        │   ├── Handler.cs
        │   └── Endpoint.cs
        └── GetById/
            ├── Query.cs
            ├── Handler.cs
            └── Endpoint.cs
```

The sample API is not documentation decoration.

It is a real compatibility test.

## Target Program.cs experience

Aim toward:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSliceForge<ApplicationAssembly>();

var app = builder.Build();

app.UseExceptionHandler();
app.MapSliceForgeEndpoints();

app.Run();
```

Exact API may evolve before 1.0.

Prioritize:

- clarity,
- discoverability,
- explicit behavior,
- minimal hidden magic.

### Milestone 5 exit condition

A developer can understand the package by opening the sample and following one vertical slice end-to-end.

Suggested commit:

```text
feat(sample): add end-to-end SliceForge consumer API
```

---

# 8. Milestone 6 — Logging & Observability

Do this only after the application pipeline is stable.

## Goals

Add:

- structured logging behavior,
- command/query activities,
- trace correlation,
- metrics,
- optional OpenTelemetry integration.

Potential future project:

```text
SliceForge.Observability
```

Do not split it into a new package until dependency pressure justifies the split.

## 8.1 LoggingBehavior

Required semantics:

```text
Success
→ Information

Expected Result failure
→ structured non-exception outcome

Cancellation
→ cancellation outcome

Unhandled exception
→ activity marked appropriately,
  exception logged once by exception owner
```

Avoid duplicate exception logging.

## 8.2 Service name

Never hardcode:

```text
Project.Api
```

Preferred default:

```text
Entry assembly name
```

Allow override:

```csharp
options.Observability.ServiceName = "DispatchFlow.Api";
```

## 8.3 Trace relationship

Target:

```text
HTTP span
└── SliceForge command/query span
    └── database/external spans
```

A shared `TraceId` should make logs and ProblemDetails correlatable.

Suggested commit:

```text
feat(observability): add structured pipeline telemetry
```

---

# 9. Milestone 7 — Configuration Model

## Goal

Support sensible defaults without forcing every capability.

Target concept:

```csharp
builder.Services.AddSliceForge<ApplicationAssembly>(options =>
{
    options.Validation.Enabled = true;
    options.Endpoints.Enabled = true;
    options.Logging.Enabled = true;
});
```

But avoid creating dozens of meaningless booleans.

Prefer feature-specific options objects.

Potential model:

```text
SliceForgeOptions
├── Validation
├── Endpoints
├── Logging
├── Observability
└── Diagnostics
```

Do not place database/authentication configuration in the core SliceForge options.

---

# 10. Milestone 8 — Package Validation

Before templates or CLI, test actual NuGet consumption.

## 10.1 Package metadata

Add professional package metadata.

Example categories:

```xml
<PackageId>SliceForge.Core</PackageId>
<Authors>Clint Villanueva</Authors>
<Description>...</Description>
<PackageTags>dotnet;vertical-slice;cqrs;aspnetcore</PackageTags>
<RepositoryUrl>...</RepositoryUrl>
<PackageReadmeFile>README.md</PackageReadmeFile>
```

Choose and document a license before public publishing.

## 10.2 Versioning

Start pre-1.0.

Example:

```text
0.1.0-preview.1
```

Do not begin at `1.0.0`.

## 10.3 Pack locally

```powershell
dotnet pack SliceForge.slnx -c Release
```

Create a local feed, for example:

```text
C:\dev\NuGetLocal
```

Install the generated packages into a clean throwaway consumer project.

Do not consider packaging complete until installation works without project references.

---

# 11. Milestone 9 — `dotnet new` Template

## Goal

Generate a complete consumer application.

Template package:

```text
templates/
└── SliceForge.Templates/
```

First target:

```bash
dotnet new sliceforge-api -n DispatchFlow
```

Expected output:

```text
DispatchFlow/
├── src/
│   └── DispatchFlow.Api/
├── tests/
│   └── DispatchFlow.Api.Tests/
└── DispatchFlow.slnx
```

## 11.1 Template naming

Use a neutral source placeholder, for example:

```text
SampleProject
```

and configure template replacement so:

```bash
-n Acme.Inventory
```

produces:

```text
Acme.Inventory.Api
Acme.Inventory.Api.Tests
Acme.Inventory.slnx
```

Do not put `SliceForge` into generated project namespaces.

## 11.2 Initial template choices

Keep the first version intentionally small.

Possible first options:

```text
--validation
--observability
--tests
```

Database/provider choices should come later unless the base template genuinely needs them.

## 11.3 Template principle

The template should compose already-tested SliceForge packages.

It must not contain a second independent copy of core infrastructure.

---

# 12. Milestone 10 — Interactive CLI

## Goal

Provide a polished interactive setup while preserving scriptability.

Project:

```text
tools/
└── SliceForge.Cli/
```

Future install:

```bash
dotnet tool install --global SliceForge.Cli
```

Future command:

```bash
sliceforge new
```

## 12.1 CLI responsibility

The CLI owns:

```text
prompts
validation
choice dependencies
generation plan
template invocation
progress output
friendly errors
```

The CLI does **not** own application architecture.

Architecture remains in packages/templates.

## 12.2 CLI structure

Suggested:

```text
SliceForge.Cli/
├── Commands/
│   ├── NewCommand.cs
│   ├── DoctorCommand.cs
│   └── VersionCommand.cs
│
├── Interactive/
│   ├── ProjectWizard.cs
│   ├── PresetPrompt.cs
│   └── ConfirmationPrompt.cs
│
├── Generation/
│   ├── GenerationPlan.cs
│   ├── ProjectGenerator.cs
│   └── TemplateArguments.cs
│
└── Program.cs
```

## 12.3 UX goal

Example:

```text
╭────────────────────────────────────────────╮
│                 SliceForge                 │
│      Modern .NET Application Builder       │
╰────────────────────────────────────────────╯

Project name
> DispatchFlow

Preset
● Recommended
○ Minimal
○ Production
○ Custom

Architecture
● Vertical Slice + MediatR
○ Vertical Slice

Validation
● FluentValidation
○ None

Observability
● Structured logging + OpenTelemetry
○ Logging only
○ None

Testing
● xUnit
○ None

──────────────────────────────────────────────

Project          DispatchFlow
Framework        .NET 10
Architecture     Vertical Slice + MediatR
Validation       FluentValidation
Observability    OpenTelemetry
Testing          xUnit

Create project? Yes
```

Then:

```text
✓ Creating solution
✓ Creating API project
✓ Creating test project
✓ Applying SliceForge packages
✓ Restoring dependencies
✓ Building solution

Project created successfully.
```

Use a mature terminal UI library only after its current compatibility/license/version has been verified.

## 12.4 Presets

Initial future presets:

```text
Recommended
Minimal
Production
Custom
```

Presets must resolve to explicit generation choices.

They must not become hidden behavior impossible for the user to inspect.

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
✓ commands discovered
✓ queries discovered
✓ handlers discovered
✓ validators discovered
✓ endpoints discovered

✗ command has no handler
✗ message has multiple handlers
⚠ internal validator scanning disabled
⚠ optional feature configured but unused
```

Startup diagnostics may also become available programmatically.

Do not build this before the basic package and template are stable.

---

# 14. Milestone 12 — Real-World Adoption

Use a real project such as DispatchFlow as a consumer.

Migration should be incremental.

Potential extracted/copied infrastructure to remove from the application:

```text
Result.cs
Error.cs
ICommand.cs
IQuery.cs
ICommandHandler.cs
IQueryHandler.cs
ValidationBehavior.cs
LoggingBehavior.cs
IEndpoint.cs
EndpointExtensions.cs
common Result → HTTP mapping
```

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
AddSliceForge<TAssemblyMarker>()
AddSliceForgeCore<TAssemblyMarker>()
AddSliceForgeValidation()
AddSliceForgeEndpoints<TAssemblyMarker>()
MapSliceForgeEndpoints()
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
SliceForge.Behaviors
SliceForge.DependencyInjection

SliceForge.AspNetCore
SliceForge.AspNetCore.Endpoints
SliceForge.AspNetCore.Results
SliceForge.AspNetCore.ProblemDetails
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

feat(core): add validation pipeline

feat(aspnetcore): introduce endpoint discovery

feat(aspnetcore): add result HTTP mapping

feat(aspnetcore): add problem details integration

feat(sample): add vertical slice sample API

feat(observability): add request pipeline telemetry

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

# 20. First Agent Session — Exact Scope

For the first implementation session, the agent should **only perform Milestone 0**.

Do not implement MediatR, FluentValidation, Result, endpoint discovery, CLI, or templates yet.

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

# 21. Next Session After Foundation Approval

After Milestone 0 is reviewed and accepted:

**Milestone 1:** implement only `Error`, `ErrorType`, `Result`, `Result<T>`, and their tests.

No MediatR yet.

This keeps the progression:

```text
Repository
    ↓
Result model
    ↓
Messaging
    ↓
Validation
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
