# EF Core Data Access

The service uses PostgreSQL through `Npgsql.EntityFrameworkCore.PostgreSQL`. `AppDbContext` receives `IOptions<DatabaseConfig>` and applies every `IEntityTypeConfiguration` beneath the configured provider namespace.

Rules:

- Register `IDbContextFactory<AppDbContext>` and Hot Chocolate’s context-factory integration.
- Resolver methods receive `AppDbContext`; never dispose a context before a returned `IQueryable` executes.
- List and export reads use `AsNoTracking`.
- Keep entity mapping in `Provider/{Entity}/Data/{Entity}Configuration.cs`.
- Declare concrete keys and audit properties on each entity. `EntityBase` is intentionally empty.
- Apply `[GraphQLIgnore]` to audit/internal properties that must not enter the schema.
- Use hard delete consistently. No query or export path applies a deleted-row predicate.
- One normal mutation uses one `SaveChangesAsync`; introduce explicit transactions only for multi-save operations.

`GridSchema` is mapped directly in `AppDbContext` to `app.gridSchema`, with `jsonb` definition storage and a unique `(EntityName, ViewName)` index.

The sample uses `EnsureCreated` for convenience, not migrations. Production services must use reviewed EF migrations and should not rely on `EnsureCreated` to evolve an existing database. This matters when source removes a column: the old database column remains until a migration drops it.
