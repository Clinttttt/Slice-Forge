# ASP.NET Result Mapping

Status: Accepted

## Context

SliceForge Results are transport-independent, while ASP.NET consumers need a
consistent mapping for expected failures. The integration must preserve the
consumer's control over successful HTTP responses and must not pull Runtime or
Validation into the ASP.NET adapter.

## Decision

`SliceForge.AspNetCore` exposes only the `ResultHttpExtensions.ToHttpResult`
overloads. Their success callbacks own the HTTP success response; SliceForge
maps expected failures as follows:

| Error type | HTTP result |
| --- | --- |
| `Failure` | 400 ProblemDetails |
| `Validation` | 400 `HttpValidationProblemDetails` |
| `NotFound` | 404 ProblemDetails |
| `Conflict` | 409 ProblemDetails |
| `Unauthorized` | `Results.Challenge()` |
| `Forbidden` | `Results.Forbid()` |

ProblemDetails expose the mapped status, `Error.Description` as `detail`, and
`Error.Code` as the `code` extension. Validation errors are copied to the
ASP.NET `Errors` dictionary, duplicate messages per property are removed in
first-seen order, and empty property keys are preserved. Core error objects
are not mutated or serialized.

HTTP 500 is reserved for unexpected exceptions, which remain exceptions and
are handled by ASP.NET Core's exception middleware. Consumer applications
configure `AddProblemDetails()` and `UseExceptionHandler()` directly; SliceForge
does not provide an `IExceptionHandler` or duplicate exception logging.
Challenge and forbid behavior is delegated to the consumer's configured
authentication system; SliceForge does not invent schemes or challenge
headers.

`SliceForge.AspNetCore` references `SliceForge.Core` and the
`Microsoft.AspNetCore.App` shared framework only. It remains a
`Microsoft.NET.Sdk` class library with an explicit framework reference.
Endpoint abstractions and endpoint discovery are deferred; consumer
applications map routes explicitly.
