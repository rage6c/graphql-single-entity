# Developer Guide

## Prerequisites

- .NET 10 SDK.
- PostgreSQL 17 or a compatible PostgreSQL server for the sample service.
- Docker Compose if using the repository database container.
- Optional SQL Server when testing generator support for MSSQL.

Check the SDK and restore the solution:

```bash
dotnet --version
dotnet restore GraphqlDataService.Sample.slnx
```

## Start A Local Environment

Start PostgreSQL:

```bash
docker compose up -d postgres
```

The Compose password is `postgres`, while the committed sample connection string uses `postgres123`. Override it when starting the service:

```bash
ConnectionStrings__Database='Host=localhost;Port=5432;Database=graphql_sample;Username=postgres;Password=postgres' \
  dotnet run --project src/GraphqlDataService.Sample
```

The application calls `EnsureCreated` outside the `Testing` environment. This creates missing tables but does not migrate or remove columns in an existing database.

Open the GraphQL IDE at the `/graphql` endpoint printed by ASP.NET Core. Health endpoints are:

- `GET /health/live` — process liveness;
- `GET /health/ready` — shared export storage readiness.

## Configuration

.NET configuration precedence applies: command line overrides environment variables, which override JSON files. Use double underscores in environment-variable names.

### GraphQL

| Key | Default in sample | Purpose |
| --- | --- | --- |
| `GraphQL:ProviderNamespacePrefix` | `GraphqlDataService.Sample.Provider` | Provider type discovery |
| `GraphQL:DefaultPageSize` | `25` | Cursor page size when omitted |
| `GraphQL:MaxPageSize` | `100` | Maximum cursor page size |
| `GraphQL:IncludeExceptionDetails` | `false` | Preserve exception details for local development |
| `GraphQL:MaxExecutionDepth` | `10` | Reserved; currently not enforced |

### Database

`Database:ProviderNamespacePrefix` controls where `AppDbContext` discovers EF configurations. It normally matches the GraphQL provider namespace.

### Export

Important keys are `SharedStorageRoot`, `DefaultFormat`, `DeleteJobFolderAfterDownload`, `MaxRows`, `MaxBytes`, `ExpiryMinutes`, `MaxConcurrentExports`, worker scan/lease/retry values, subscription polling, and `CleanupIntervalMinutes`.

`LeaseSeconds` must be at least three times `WorkerScanIntervalSeconds`. Every application server must mount the same durable `SharedStorageRoot`.

## Provision The Default Customer Grid

Customer list queries do not require grid metadata, but both export queries and `gridDefinition` do. The current source has no Customer grid seed.

After the service creates `app."gridSchema"`, provision an active default definition. The exact SQL may need adjustment if your naming convention differs:

```sql
INSERT INTO app."gridSchema"
    ("Id", "EntityName", "ViewName", "Definition", "Version", "IsActive",
     "CreatedAt", "UpdatedAt", "CreatedBy")
VALUES
    (gen_random_uuid(), 'Customer', 'default',
     '{
       "entityName":"Customer",
       "gridViewName":"default",
       "exportFormat":null,
       "columns":[
         {"columnName":"Id","columnType":6,"displayFormat":null,"textAlignment":0,"visibility":true,"width":280,"enableFiltering":true,"enableSorting":true,"sourceGraphqlColumn":"id"},
         {"columnName":"Name","columnType":0,"displayFormat":null,"textAlignment":0,"visibility":true,"width":220,"enableFiltering":true,"enableSorting":true,"sourceGraphqlColumn":"name"},
         {"columnName":"Email","columnType":0,"displayFormat":null,"textAlignment":0,"visibility":true,"width":280,"enableFiltering":true,"enableSorting":true,"sourceGraphqlColumn":"email"},
         {"columnName":"Birth Date","columnType":4,"displayFormat":"yyyy-MM-dd","textAlignment":1,"visibility":true,"width":140,"enableFiltering":true,"enableSorting":true,"sourceGraphqlColumn":"birthDate"}
       ]
     }'::jsonb,
     1, true, now(), now(), 'developer')
ON CONFLICT ("EntityName", "ViewName") DO NOTHING;
```

The enum values above follow `GridColumnType` and `TextAlignment` as currently serialized by `System.Text.Json`.

## Use The GraphQL API

### Page Customers

```graphql
query GetCustomers($first: Int, $after: String) {
  customers(first: $first, after: $after, order: [{ name: ASC }]) {
    totalCount
    nodes { id name email birthDate }
    pageInfo { hasNextPage hasPreviousPage startCursor endCursor }
  }
}
```

Cursor values are opaque. To fetch page three, retain page two’s `endCursor` and supply it as `after`.

### Create A Customer

```graphql
mutation CreateCustomer($input: CustomerCreateInput!) {
  createCustomer(input: $input) { id name email birthDate }
}
```

```json
{
  "input": {
    "name": "Ada Lovelace",
    "email": "ada@example.test",
    "birthDate": "1815-12-10"
  }
}
```

### Export Customers

```graphql
query ExportCustomers {
  downloadCustomers(
    format: CSV
    columns: ["id", "name", "email"]
    filters: [{ field: "name", operator: CONTAINS, value: "Ada" }]
    order: [{ field: "name", direction: ASCENDING }]
  ) {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

Use `downloadCustomersStatus(exportId: UUID!)` over WebSockets to observe status changes. Do not embed extra quotes in the UUID variable. Download the completed file from the returned relative URL.

More requests and responses are in [Customer API examples](customer-api-examples.md).

## Add A Provider With The Generator

The generator reads live metadata and writes provider files:

```bash
dotnet run --project src/GraphqlDataService.Generator -- \
  --Generator:Provider=PostgreSql \
  --Generator:ConnectionString="$DATABASE_CONNECTION_STRING" \
  --Generator:Namespace=GraphqlDataService.Sample \
  --Generator:OutputPath=src/GraphqlDataService.Sample/Provider \
  --Generator:Schema=app \
  --Generator:Tables:0=orders
```

For SQL Server use `SqlServer` or `Mssql` and the corresponding connection string/schema.

Before generation:

- ensure the table has exactly one primary key;
- use an identity numeric key or a non-identity `Guid` key;
- commit or back up hand-written provider changes because generation overwrites destination files.

After generation:

1. Review singular/plural naming.
2. Remove fields that must not be exposed.
3. Add indexes and relationships missing from generated EF configuration.
4. Add business validation to the mapper.
5. Add or provision the entity’s default grid definition.
6. Build and inspect the generated GraphQL schema.
7. Add mapper, schema, filter/order, and export tests.

The generator reflects the live database. If a removed source property still exists as a stale database column, it will be regenerated. Apply migrations before regenerating.

## Add A Provider Manually

Create `Provider/{Entity}/Data/{Entity}.cs` deriving from the empty `EntityBase`, then create an `IEntityTypeConfiguration<TEntity>` beside it. A `DbSet<TEntity>` property is optional because shared code uses `db.Set<TEntity>()`.

Add these provider components:

1. Create/update input records using `Optional<T>` for update fields.
2. `ICrudMapper<TEntity,TKey,TCreateInput,TUpdateInput>` implementation.
3. Query derived from `CrudQuery<TEntity>` with paging, projection, typed filtering, and typed sorting.
4. Mutation derived from `CrudMutation<TEntity,TKey,TCreateInput,TUpdateInput,TMapper>`.
5. Subscription derived from `CrudSubscription<TEntity>`.
6. Filter and sort input types.
7. One `EntityExportField` definition per exposed field through entity query capabilities.
8. Export generator derived from `CrudExportGenerator<TEntity>`.

Keep the namespace beneath both configured provider prefixes. No `Program.cs` registration is needed; GraphQL and EF discover the provider by convention.

## Field Capabilities

Define exportable fields once in `{Entity}QueryCapabilities`. The same typed definitions drive:

- GraphQL filter binding;
- GraphQL sort binding;
- export filter/order validation;
- server-side export filtering and ordering;
- projection property mapping;
- invariant CSV/Excel formatting.

Always include the stable primary-key field. Use only operators that EF can translate for the selected property type.

## Relationships And N+1 Queries

The current Customer schema is flat. When adding relationships:

- prefer projection-compatible EF navigation fields for simple nested selections;
- use Hot Chocolate DataLoaders for fields requiring separate database or service calls;
- do not query a relationship independently once per parent resolver;
- test SQL query counts for representative nested operations;
- consider split-query behavior for multiple collection navigations.

`[UseProjection]` reduces over-fetching but is not a universal N+1 solution.

## Database Changes

The sample’s `EnsureCreated` behavior is for local convenience only. It does not update existing tables. Use EF migrations in production and for any schema evolution that must remove or rename columns.

Keep the database and entity model synchronized before running the generator. Explicitly review:

- keys and identity strategy;
- column names and lengths;
- unique and performance indexes;
- nullability;
- relationships and delete behavior;
- audit/internal fields excluded from GraphQL.

## Test And Verify

```bash
dotnet restore GraphqlDataService.Sample.slnx
dotnet build GraphqlDataService.Sample.slnx --no-restore
dotnet test GraphqlDataService.Sample.slnx --no-build --no-restore
dotnet list package --vulnerable --include-transitive
```

Run the repeatable 90% production-code coverage gate with:

```bash
./scripts/coverage.sh
```

Composition-root top-level programs are excluded with `ExcludeFromCodeCoverage`; service, generator, and infrastructure code remain included.

Warnings are treated as errors. Update package lock files whenever dependencies change.

## Troubleshooting

### `GRID_DEFINITION_NOT_FOUND`

Create an active `{Entity}/default` row in `app.gridSchema` or correct the requested view name.

### `No exporter is registered`

Confirm the exporter implements `IExportGenerator`, resides beneath `GraphQL:ProviderNamespacePrefix`, and its entity name matches the job record.

### Subscription reports an invalid UUID argument

Send a normal GraphQL UUID string or variable. Do not pass a string containing escaped quote characters.

### Duplicate email returns `INTERNAL_SERVER_ERROR`

The database unique index is working, but generic CRUD does not yet translate provider-specific uniqueness exceptions. Check server logs and add an entity-specific stable error mapping if the client contract requires one.

### Generated Customer contains `DeletedAt`

The live table still has that column. `EnsureCreated` cannot remove it; apply a migration or alter the database before regenerating.

### Export remains queued

Check that the worker is running, shared storage is writable, `job.yaml` is valid, an exporter is registered, and no stale `claim.lock` remains within a valid lease.

## Security Checklist Before Production

- Add authentication and GraphQL/REST authorization.
- Enforce entity policies and export ownership/tenant isolation.
- Store connection strings outside committed JSON.
- Disable exception details.
- Add query depth, cost, timeout, and introspection controls.
- Use migrations instead of `EnsureCreated`.
- Verify shared-storage locking across application servers.
- Add database/schema/grid readiness checks and operational telemetry.
