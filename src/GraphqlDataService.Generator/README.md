# GraphQL Data Service Generator

This console application reads PostgreSQL or SQL Server table metadata through an EF Core provider and renders provider source files with Scriban.

## Configuration

Configure `Generator` in `appsettings.json`, environment variables, or command-line arguments:

```json
{
  "Generator": {
    "Provider": "SqlServer",
    "ConnectionString": "Server=localhost,1433;Database=app;User Id=sa;Password=secret;Encrypt=True;TrustServerCertificate=True",
    "Namespace": "GraphqlDataService.Sample",
    "OutputPath": "../GraphqlDataService.Sample/Provider",
    "Schema": "app",
    "Tables": ["customers"]
  }
}
```

`Provider` accepts `PostgreSql`/`Postgres` and `SqlServer`/`Mssql`. An empty `Tables` array generates every table in the configured schema.

Keep connection strings outside committed configuration. Environment variables use standard .NET configuration names:

```bash
export Generator__Provider=SqlServer
export Generator__ConnectionString='Server=localhost,1433;Database=app;User Id=sa;Password=secret;Encrypt=True;TrustServerCertificate=True'
export Generator__Namespace=GraphqlDataService.Sample
export Generator__OutputPath=../GraphqlDataService.Sample/Provider
export Generator__Schema=app
dotnet run --project src/GraphqlDataService.Generator
```

Command-line values override JSON configuration:

```bash
dotnet run --project src/GraphqlDataService.Generator -- \
  --Generator:Provider=SqlServer \
  --Generator:ConnectionString='Server=localhost;Database=app;User Id=sa;Password=secret;TrustServerCertificate=true' \
  --Generator:Namespace=GraphqlDataService.Sample \
  --Generator:OutputPath=src/GraphqlDataService.Sample/Provider \
  --Generator:Schema=dbo \
  --Generator:Tables:0=customers
```

## Conventions

- Each table produces its own provider folder and `Data` subfolder.
- Tables must have exactly one primary key.
- Identity `int`/`long` keys and application-generated `Guid` keys are supported.
- Unknown database types and composite keys stop generation with a descriptive error.
- Existing generated files are overwritten; review changes before committing.
- Metadata discovery currently covers columns, nullability, primary keys, identity, and maximum lengths. Non-primary indexes such as unique email indexes are not generated.
- The live database is authoritative to the generator. Stale columns left behind by `EnsureCreated` or missing migrations will appear in generated source.
