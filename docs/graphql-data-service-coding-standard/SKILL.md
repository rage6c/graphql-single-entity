---
name: graphql-data-service-coding-standard
description: Source-aligned standard for this .NET Hot Chocolate data service and its EF Core/Scriban provider generator.
---

# GraphQL Data Service Coding Standard

1. Treat `src` as authoritative and read [README.md](README.md).
2. Load the narrow topic files needed for the task.
3. Build the solution and run targeted tests after code changes.
4. Run the package vulnerability audit after dependency changes.

## Non-Negotiable Source Conventions

- Provider folder per entity with mapping under `Data`.
- `CrudQuery<TEntity>` for lists/exports.
- `CrudMutation<TEntity,TKey,TCreateInput,TUpdateInput,TMapper>` for hard-delete CRUD.
- Empty `EntityBase`; concrete keys/audit properties.
- Paging, projection, explicit filtering, explicit sorting.
- Provider discovery by configurable namespace, not manual Customer registration.
- Database-backed grid definitions.
- UUID-folder/YAML export jobs, status subscriptions, and REST file downloads.
- Authentication is currently absent and must be identified as a production gap.
- PostgreSQL/SQL Server source generation uses EF Core metadata readers and Scriban templates.

Use [15-review-checklist-examples.md](15-review-checklist-examples.md) for reviews and [17-source-generator.md](17-source-generator.md) for generator work.
