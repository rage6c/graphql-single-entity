# Async, Performance, And Security

- Propagate `CancellationToken` through EF, job-store, generator, and file operations.
- Return `IQueryable` synchronously from list resolvers; do not wrap query composition in `Task.Run`.
- Page list fields and project before materialization.
- Bind filter/sort fields explicitly through typed entity field definitions.
- Export only selected columns and enforce `MaxRows` and `MaxBytes`.
- Prevent spreadsheet formula injection in CSV and Excel values.
- Keep export folders beneath the canonical shared root and use UUID directory names.

The subscription implementation polls YAML status at `StatusPollingIntervalSeconds` and emits only status changes until a terminal state. It does not use entity snapshots or Hot Chocolate topics.

Current limitations that must not be overstated in documentation:

- `MaxExecutionDepth` is configured but not enforced.
- No cost analysis, execution timeout, persisted-operation policy, or introspection restriction is configured.
- Authentication, authorization, ownership checks, and tenant isolation are absent.
