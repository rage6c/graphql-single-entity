# REST API Coding Standard

Focused topic files for the C# REST API coding standard.

This repository currently implements a GraphQL data service, not a REST CRUD service. This folder remains general reference material; use [the GraphQL standard](../graphql-data-service-coding-standard/README.md) and `src` for source-aligned decisions. The only REST surface currently implemented is the export file download endpoint.

Use this folder as the source of truth for REST API implementation patterns. Load only the topic files needed for the current change so LLM context stays focused.

Use [SKILL.md](SKILL.md) as the skill entry point when applying this standard through Codex.

## Current Key Decisions

- Keep controllers thin and put business rules in services.
- Keep DTOs separate from EF entities.
- Load configuration into typed `Config` objects, validate at startup, and register them as singletons.
- Use Fluent API for EF mapping.
- Define `HasDefaultSchema(...)` in every `DbContext`.
- Define `ToTable("TableName", schemaName)` for every mapped table.
- Keep EF entities free of EF mapping annotations.
- Write logs to both console and daily/size-rolled files.
- Do not add access-control middleware unless a future standard explicitly introduces it.
- Use `xunit.v3` for unit tests.

| File | Contents |
| --- | --- |
| [01-project-structure-naming.md](01-project-structure-naming.md) | Project structure and naming conventions |
| [02-configuration.md](02-configuration.md) | Strongly typed configuration objects, options pattern, environment config, and startup validation |
| [03-controllers-requests-responses.md](03-controllers-requests-responses.md) | Controller rules, API request inputs, and API response patterns |
| [04-contracts-services-linq-transactions.md](04-contracts-services-linq-transactions.md) | Data contracts, service contracts, business services, LINQ, and transactions |
| [05-ef-exceptions-validation.md](05-ef-exceptions-validation.md) | EF Core Fluent API mapping, mandatory schemas, exception handling, and validation |
| [06-dependency-injection.md](06-dependency-injection.md) | DI lifetimes, captive dependencies, keyed services, Scrutor, and open generics |
| [07-integration-clients.md](07-integration-clients.md) | Outbound REST, GraphQL, gRPC clients, typed HttpClient, resilience, and correlation ID |
| [08-logging.md](08-logging.md) | Structured logging, Serilog configuration, enrichers, log levels, and aggregation |
| [09-program-cs.md](09-program-cs.md) | Program.cs composition root, DI, middleware ordering, configuration, and startup rules |
| [10-async-and-performance.md](10-async-and-performance.md) | Async method signatures, CancellationToken propagation, database performance, and response compression |
| [11-formatting-and-style.md](11-formatting-and-style.md) | EditorConfig, .NET analyzers, nullable reference types, file organization, and style rules |
| [12-api-versioning.md](12-api-versioning.md) | URL path versioning, Asp.Versioning.Mvc, deprecation, and sunset policies |
| [13-openapi-documentation.md](13-openapi-documentation.md) | OpenAPI registration, schema customization, operation metadata, and package choice |
| [14-health-checks-background-services.md](14-health-checks-background-services.md) | Liveness and readiness probes, health check implementations, background services, and graceful shutdown |
| [15-unit-testing-xunit.md](15-unit-testing-xunit.md) | Unit testing with xUnit, Moq, controller tests, service tests, and coverage requirements |
| [16-package-version-vulnerability-checks.md](16-package-version-vulnerability-checks.md) | NuGet package version, deprecation, vulnerability, and lock-file checks |
| [17-review-checklist-examples.md](17-review-checklist-examples.md) | Review checklist with good and bad code snippets covering all topics |
| [18-dotnet-java-comparison.md](18-dotnet-java-comparison.md) | Mapping of this standard to Java, Spring Boot, and Spring Data JPA |

## Suggested Loading Strategy

- Controller or endpoint work: `01`, `03`, `17`.
- Service-layer work: `01`, `04`, `17`.
- EF changes: `01`, `05`, `17`.
- Configuration or `Program.cs` work: `02`, `06`, `09`, `17`.
- Outbound integration work: `01`, `07`, `17`.
- Logging or operational review: `08`, `14`, `17`.
- Unit test work: `04`, `10`, `15`, `17`.
- Dependency updates or vulnerability review: `16`, `17`.
- .NET and Java comparison or migration planning: `18` plus the relevant implementation topic files.
