---
name: rest-api-coding-standard
description: C#/.NET REST API coding standard for EF Core-backed services. Load when creating, reviewing, or updating ASP.NET Core API code in this repository, or when comparing these rules with Java and Spring.
---

# REST API Coding Standard

Entry point for applying the repository coding standard. Load topic files below as needed.

## Workflow

1. Read [README.md](README.md) first.
2. Select the narrowest topic files for the task.
3. Apply the standard before editing.
4. Prefer existing repository patterns where they already follow the standard.
5. Run `dotnet build`, targeted `dotnet test`, and package vulnerability checks after changes.

## Topic Routing

- [01-project-structure-naming.md](01-project-structure-naming.md) — Project layout and naming
- [02-configuration.md](02-configuration.md) — Typed configuration, options pattern, environment config
- [03-controllers-requests-responses.md](03-controllers-requests-responses.md) — Controllers, inputs, and response patterns
- [04-contracts-services-linq-transactions.md](04-contracts-services-linq-transactions.md) — DTOs, services, LINQ rules, and transactions
- [05-ef-exceptions-validation.md](05-ef-exceptions-validation.md) — EF Core mapping, exception handling, and validation
- [06-dependency-injection.md](06-dependency-injection.md) — DI lifetimes, captive dependencies, and registration
- [07-integration-clients.md](07-integration-clients.md) — Outbound API clients, resilience, and correlation
- [08-logging.md](08-logging.md) — Structured logging, enrichers, and log levels
- [09-program-cs.md](09-program-cs.md) — Program.cs composition root and middleware order
- [10-async-and-performance.md](10-async-and-performance.md) — Async patterns, CancellationToken, and performance
- [11-formatting-and-style.md](11-formatting-and-style.md) — EditorConfig, analyzers, and style
- [12-api-versioning.md](12-api-versioning.md) — URL path versioning and deprecation
- [13-openapi-documentation.md](13-openapi-documentation.md) — OpenAPI registration and customization
- [14-health-checks-background-services.md](14-health-checks-background-services.md) — Health checks and background services
- [15-unit-testing-xunit.md](15-unit-testing-xunit.md) — xUnit testing, Moq, and coverage requirements
- [16-package-version-vulnerability-checks.md](16-package-version-vulnerability-checks.md) — NuGet vulnerability and version checks
- [17-review-checklist-examples.md](17-review-checklist-examples.md) — Merge review checklist with good and bad examples
- [18-dotnet-java-comparison.md](18-dotnet-java-comparison.md) — Comparison with Java, Spring Boot, and Spring Data JPA

## Non-Negotiable Defaults

- Thin controllers, DTOs separate from EF entities, business rules in services.
- Typed configuration validated at startup, registered as singletons.
- EF Core Fluent API mapping with `HasDefaultSchema` and `ToTable` per table. Entities free of mapping annotations.
- `AsNoTracking()` for reads. `Expression<Func<...>>` predicates and selectors for EF query composition. `ids.Contains(entity.Id)` for `WHERE IN`.
- LINQ terminal operations in the owning layer. Transactions for multi-step consistency.
- Console + rolling file logging (daily, 10 MB per file). Structured placeholders only.
- Typed outbound clients with timeouts, resilience, and correlation propagation.
- `xunit.v3`, mock external dependencies, coverage: overall 80%, new code 90%.
- `ProblemDetails` for exceptions. Input validation before business operations.
- Package vulnerability and deprecation checks when dependencies change.

## Review Posture

Lead with findings ordered by severity and include file/line references. Check [17-review-checklist-examples.md](17-review-checklist-examples.md) before summarizing.
