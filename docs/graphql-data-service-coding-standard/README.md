# GraphQL Data Service Coding Standard

Source-aligned guidance for the .NET 10 Hot Chocolate data service in this repository. The implementation under `src` is authoritative when examples and code differ.

## Current Decisions

- Use Hot Chocolate 15.1.17 packages at one version and EF Core 9.0.4.
- Put each entity in `Provider/{Entity}` with entity mapping in `Provider/{Entity}/Data`.
- Derive list providers from `CrudQuery<TEntity>` and use paging, projection, explicit filtering, and explicit sorting.
- Derive mutations from `CrudMutation<TEntity,TKey,TCreateInput,TUpdateInput,TMapper>`.
- Keep `EntityBase` empty; keys and audit properties belong to concrete entities.
- Support single `Guid`, `int`, `long`, or other EF-compatible keys through `TKey`; use hard delete.
- Discover provider type extensions, mappers, export capabilities, and exporters beneath configured provider namespaces.
- Store grid definitions in PostgreSQL `app.gridSchema`; the database is the runtime source of truth.
- Run exports as shared-folder jobs and serve completed files through `GET /exports/{exportId}/download`.
- Authentication is disabled in the sample. Production deployments must add it together with entity and export ownership policies.
- Generate provider scaffolding from PostgreSQL or SQL Server with the EF Core/Scriban console generator.

| File | Contents |
| --- | --- |
| [01-project-structure-naming.md](01-project-structure-naming.md) | Actual project and provider structure |
| [02-packages-configuration.md](02-packages-configuration.md) | Packages and typed options |
| [03-query-providers.md](03-query-providers.md) | `CrudQuery`, middleware, and paging |
| [04-crud-mutations.md](04-crud-mutations.md) | Key-generic hard-delete CRUD |
| [05-grid-definitions.md](05-grid-definitions.md) | Database-backed grid definitions |
| [06-ef-core-data-access.md](06-ef-core-data-access.md) | EF contexts, mapping, and hard delete |
| [07-dependency-injection-program.md](07-dependency-injection-program.md) | Dynamic provider discovery and startup |
| [08-authorization-errors-validation.md](08-authorization-errors-validation.md) | Current error behavior and production security gap |
| [09-logging-observability.md](09-logging-observability.md) | Serilog and export diagnostics |
| [10-async-performance-security.md](10-async-performance-security.md) | Query/export limits and cancellation |
| [11-testing-xunit.md](11-testing-xunit.md) | Current test structure and expectations |
| [12-formatting-style.md](12-formatting-style.md) | Provider and C# conventions |
| [13-operations-health-background.md](13-operations-health-background.md) | Health checks, workers, and startup |
| [14-package-vulnerability-checks.md](14-package-vulnerability-checks.md) | Central versions, lock files, and audit commands |
| [15-review-checklist-examples.md](15-review-checklist-examples.md) | Source-aligned review checklist |
| [16-export-downloads.md](16-export-downloads.md) | Export jobs, subscriptions, files, and REST download |
| [17-source-generator.md](17-source-generator.md) | PostgreSQL/SQL Server provider generation |

The REST standard remains general .NET reference material. It does not describe this GraphQL service’s concrete structure.
