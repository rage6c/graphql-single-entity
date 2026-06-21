# Database Provider Source Generator

`src/GraphqlDataService.Generator` is a .NET console application using EF Core database providers to open the configured database and Scriban templates to render provider files.

## Configuration

| Key | Meaning |
| --- | --- |
| `Generator:Provider` | `PostgreSql`/`Postgres` or `SqlServer`/`Mssql` |
| `Generator:ConnectionString` | Database connection string |
| `Generator:Namespace` | Application root namespace, such as `GraphqlDataService.Sample` |
| `Generator:OutputPath` | Parent provider directory |
| `Generator:Schema` | Database schema to inspect |
| `Generator:Tables` | Optional table allowlist; empty means every table in the schema |

Environment variables use double underscores. Command-line keys use colons.

## Generated Files

For each table, the generator writes entity/configuration, inputs, mapper, query, mutation, subscription, filter/sort types, query capabilities, and export generator beneath `{OutputPath}/{Entity}`.

The table must have exactly one primary key. Identity numeric keys are left for the database; non-identity `Guid` keys use `Guid.NewGuid`. Composite keys, non-generated non-Guid keys, and unknown database types stop generation.

PostgreSQL metadata comes from `information_schema` with `udt_name`; SQL Server metadata uses `INFORMATION_SCHEMA` plus `COLUMNPROPERTY` for identity detection.

## Review Generated Output

- Existing destination files are overwritten.
- The generator reflects the live database, including stale columns that source may have removed.
- It currently reads columns, nullability, primary keys, identity, and maximum length. It does not reproduce unique/non-primary indexes such as Customer email uniqueness.
- Naming uses a small built-in English singularizer/pluralizer; irregular table names require review.
- Run formatting, build, schema tests, and a database-backed smoke test after generation.

Example:

```bash
dotnet run --project src/GraphqlDataService.Generator -- \
  --Generator:Provider=PostgreSql \
  --Generator:ConnectionString="$DATABASE_CONNECTION_STRING" \
  --Generator:Namespace=GraphqlDataService.Sample \
  --Generator:OutputPath=src/GraphqlDataService.Sample/Provider \
  --Generator:Schema=app \
  --Generator:Tables:0=customers
```
