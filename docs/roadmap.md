# SliceForge Roadmap

The implementation is intentionally incremental:

1. Repository foundation — completed
2. Core result and error model — completed
3. Messaging abstractions — completed
4. Runtime execution and explicit handler registration — completed
5. Validation — completed
6. ASP.NET Core Result mapping — completed
7. Sample consumer API — next
8. Logging and observability
9. Configuration and package validation
10. `dotnet new` template
11. Interactive CLI
12. Diagnostics
13. Real-world adoption

Milestone 5 adds Core-only Result-to-HTTP mapping in `SliceForge.AspNetCore`.
Consumers own successful response semantics, explicit endpoint mapping,
authentication configuration, and ASP.NET Core exception middleware setup.
Endpoint discovery remains deferred. Validation is an optional sender
decorator outside Core and Runtime; Runtime itself remains free of validation,
logging, and mediator dependencies.
