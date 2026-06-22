# Packages And Configuration

## Package Baseline

The repository centrally pins packages in `Directory.Packages.props` and locks restore results per project.

- Hot Chocolate packages: 15.1.17.
- EF Core and SQL Server provider: 9.0.4. Npgsql remains in the generator for PostgreSQL metadata support.
- ClosedXML: Excel generation.
- YamlDotNet: export job records.
- Scriban 7.2.4 and EF SQL Server: source generator only.
- Serilog: request and rolling-file logging.

Do not add per-project versions. Keep every `HotChocolate.*` reference aligned.

## Typed Options

`GraphQlConfig` controls provider namespace discovery, default/max page sizes, and exception details. `DatabaseConfig` independently controls the namespace scanned for `IEntityTypeConfiguration` classes. `ExportConfig` controls shared storage, limits, expiry, worker timing, retries, polling, and post-download deletion.

All three option types are bound and validated at startup. `ExportConfig.LeaseSeconds` must exceed `WorkerScanIntervalSeconds`; `GraphQlConfig.DefaultPageSize` must not exceed `MaxPageSize`.

`MaxExecutionDepth` is currently represented in configuration but is not applied by `GraphQlConfiguration`; do not claim depth enforcement until it is wired into request execution.

## Secrets And Provider Generator

Keep service and generator connection strings in environment variables or a secret store. The generator uses the standard keys `Generator__Provider`, `Generator__ConnectionString`, `Generator__Namespace`, `Generator__OutputPath`, `Generator__Schema`, and indexed `Generator__Tables__0` values.
