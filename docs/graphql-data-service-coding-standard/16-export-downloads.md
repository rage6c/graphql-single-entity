# Entity Export Downloads

## GraphQL Operations

Each entity query creates jobs through:

```graphql
downloadCustomers(
  format: ExportFileFormat!
  columns: [String!]!
  filters: [ExportFilterInput!]
  order: [ExportOrderInput!]
): ExportJob!

downloadCustomersByGridView(gridViewName: String!): ExportJob!
```

The first operation still loads the entity’s active `default` grid to validate selected columns and filter/sort permissions. The second uses visible columns, filters, order, and optional format from the named grid.

## Shared Field Definitions

`EntityExportField<TEntity,TValue>` stores the GraphQL field name, CLR property selector, allowed operators, filter function, order selector, and invariant formatter. `EntityExportQueryCapabilities<TEntity>` uses the same definitions for schema binding, validation, export filtering/order, projection property resolution, and formatting.

`CrudExportGenerator<TEntity>` reads with `AsNoTracking`, applies validated filters and deterministic ordering, projects selected entity properties server-side, enforces row/byte limits, and writes CSV or XLSX. CSV and Excel values beginning with `=`, `+`, `-`, or `@` are prefixed to prevent spreadsheet formula execution.

## Job Storage And Worker

Each job uses `{SharedStorageRoot}/{exportId:D}` containing `job.yaml` and, after success, the export file. `YamlExportJobStore` owns canonical paths, atomic YAML writes, claims, stale lease recovery, and retry limits.

The worker transitions eligible jobs through claimed/running to completed or failed, records server/timestamps/file metadata, and selects an exporter by `EntityName`. `MaxConcurrentExports` bounds parallel job generation.

## Status Subscription

`downloadCustomersStatus(exportId: UUID!)` polls the YAML record and emits changes until completed, failed, downloaded, or expired. A completed update includes `/exports/{exportId}/download`.

## REST Download

`GET /exports/{exportId:guid}/download` returns:

- `202 Accepted` and `Retry-After: 5` for queued/claimed/running;
- the recorded file for completed jobs after existence and byte-size checks;
- `422` with a sanitized code for failed jobs;
- `404` for missing, expired, non-completed, or inconsistent jobs.

Responses use `Cache-Control: no-store`. When `DeleteJobFolderAfterDownload` is true, folder deletion is registered with `Response.OnCompleted`; otherwise cleanup removes it after expiry.

## Security State

The sample does not authenticate job creation, status, or download and does not enforce `RequestedBy`/`TenantId`. Production must add owner/tenant authorization before exposing the endpoint.
