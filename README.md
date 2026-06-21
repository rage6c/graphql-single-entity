# GraphQL Data Service Sample

.NET 10 sample data service built with Hot Chocolate 15, EF Core, PostgreSQL, asynchronous CSV/Excel exports, and a schema-driven provider generator.

## Projects

- `src/GraphqlDataService.Sample` — GraphQL service and REST export download endpoint.
- `src/GraphqlDataService.Generator` — EF Core and Scriban console generator for PostgreSQL and SQL Server tables.
- `tests/GraphqlDataService.Sample.Tests` — schema, mapper, export-store, generator, and model tests.

Each exposed table owns a folder under `Provider/{Entity}`. The Customer provider contains its entity mapping, query, mutation, subscription, inputs, mapper, filtering/sorting capabilities, and export generator. Shared behavior lives under `GraphQL`.

## Run PostgreSQL And The Service

The Docker Compose password is `postgres`; override the sample appsetting when running locally:

```bash
docker compose up -d
ConnectionStrings__Database='Host=localhost;Port=5432;Database=graphql_sample;Username=postgres;Password=postgres' \
  dotnet run --project src/GraphqlDataService.Sample
```

Open `/graphql`. Authentication and authorization are intentionally disabled in this sample.

`DatabaseInitializer` calls `EnsureCreated` and can seed implementations of `IEntityGridDefinition`. The current Customer provider does not include a code seed, so a fresh database must contain an active `Customer/default` row in `app.gridSchema` before `gridDefinition` or either export query can succeed.

## GraphQL Example

```graphql
query GetCustomers {
  customers(first: 25, order: [{ name: ASC }]) {
    totalCount
    nodes { id name email birthDate }
    pageInfo { hasNextPage endCursor }
  }
}
```

Create an asynchronous export job:

```graphql
query ExportCustomers {
  downloadCustomers(format: CSV, columns: ["id", "name", "email"]) {
    exportId
    status
    downloadUrl
    expiresUtc
  }
}
```

The worker stores `job.yaml` and the generated file under `Export:SharedStorageRoot/{exportId}`. After completion, download it from `GET /exports/{exportId}/download`. See [Customer API examples](docs/customer-api-examples.md).

## Generate Providers From Database Tables

The generator supports `PostgreSql`/`Postgres` and `SqlServer`/`Mssql`:

```bash
dotnet run --project src/GraphqlDataService.Generator -- \
  --Generator:Provider=PostgreSql \
  --Generator:ConnectionString='Host=localhost;Port=5432;Database=graphql_sample;Username=postgres;Password=postgres' \
  --Generator:Namespace=GraphqlDataService.Sample \
  --Generator:OutputPath=src/GraphqlDataService.Sample/Provider \
  --Generator:Schema=app \
  --Generator:Tables:0=customers
```

The generator requires one primary key per table. It supports database-generated numeric keys and application-generated `Guid` keys. Review generated changes before committing; files in the destination entity folder are overwritten. See the [generator README](src/GraphqlDataService.Generator/README.md).

## Verify

```bash
dotnet restore GraphqlDataService.Sample.slnx
dotnet build GraphqlDataService.Sample.slnx --no-restore
dotnet test GraphqlDataService.Sample.slnx --no-build --no-restore
dotnet list package --vulnerable --include-transitive
```

## Load Sample Data

With the service running:

```bash
./scripts/create-1000-customers.sh
```

Override the endpoint with `GRAPHQL_URL` when necessary.

Before production use, add authentication, entity policies, tenant/owner checks for exports, database migrations, and durable shared export storage.
