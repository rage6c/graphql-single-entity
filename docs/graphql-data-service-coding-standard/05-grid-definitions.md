# Grid Definitions

One shared query resolves every entity grid:

```graphql
gridDefinition(entityName: String!, gridViewName: String = "default"): GridDefinition!
```

`GridColumnDefinition` includes column name/type, display format, text alignment, visibility, width, filtering/sorting flags, and the source GraphQL column.

## Runtime Source

`EntityGridDefinitionRegistry` reads active rows from `AppDbContext.GridSchemas` with `AsNoTracking`. Entity and view matching are case-insensitive. The `definition` JSON is deserialized using web JSON conventions.

The EF entity maps to PostgreSQL table `app.gridSchema`; `definition` is `jsonb`, and `(EntityName, ViewName)` has a unique index. Missing/inactive rows return `GRID_DEFINITION_NOT_FOUND`; malformed JSON returns `GRID_DEFINITION_INVALID`.

`DatabaseInitializer` accepts `IEnumerable<IEntityGridDefinition>` and inserts missing seeds after `EnsureCreated`. The current Customer provider does not implement or register a seed. Therefore a fresh deployment must provision an active `Customer/default` database row before grid lookup or exports are used.

Rules:

- `sourceGraphqlColumn` must match a registered entity field definition.
- Explicit export columns must exist in the default grid.
- Named-view exports use visible columns, stored filters/order, and the view’s export format or the configured default.
- Filtering and sorting flags are validated again when an export is enqueued.
- `GridSchema` is infrastructure data and must not receive generic CRUD fields.
- The current registry has no cache; do not document cache invalidation until one is implemented.
