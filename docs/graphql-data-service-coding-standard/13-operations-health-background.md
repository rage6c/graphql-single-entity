# Health Checks And Background Services

The service exposes:

- `/health/live` with no dependency checks;
- `/health/ready` with `ExportStorageHealthCheck` tagged `ready`.

Readiness currently validates shared export storage only. It does not check PostgreSQL connectivity, GraphQL schema construction, or grid definitions.

`ExportJobWorker` uses `PeriodicTimer`, scans YAML jobs, atomically claims eligible work through `IExportJobStore`, records `Environment.MachineName`, creates a scope for the matching `IExportGenerator`, and writes completed or sanitized failed state.

`ExportCleanupService` removes expired non-active job folders. It skips claimed, running, and downloading records.

Operational rules:

- Honor `stoppingToken` and rethrow cancellation requested during shutdown.
- Resolve scoped exporters inside a worker scope.
- Never expose worker exception details in YAML or GraphQL.
- Shared storage must provide the locking and atomic replacement semantics required by `YamlExportJobStore`.
- Add database/schema/grid readiness and metrics before production.
