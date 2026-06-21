# Authorization, Errors, And Validation

## Current Security State

Authentication and authorization are disabled. Provider fields, grid metadata, export job creation/status, and REST downloads are anonymous. Export records use `RequestedBy: anonymous` and `TenantId: null`.

This is acceptable only for the local sample. Before production, add authentication middleware, Hot Chocolate authorization, entity read/write/delete policies, and ownership/tenant checks to job creation, subscription, and download.

## Implemented Error Codes

- `NOT_FOUND` — generic CRUD lookup failed.
- `VALIDATION_FAILED` — mapper validation failed.
- `GRID_DEFINITION_NOT_FOUND` and `GRID_DEFINITION_INVALID`.
- `EXPORT_COLUMNS_REQUIRED`, `EXPORT_COLUMN_DUPLICATE`, `EXPORT_COLUMN_INVALID`.
- `EXPORT_FILTER_INVALID`, `EXPORT_ORDER_INVALID`, `EXPORT_LIMIT_EXCEEDED`.
- `EXPORT_JOB_NOT_FOUND`, `EXPORT_FAILED`, and `INTERNAL_SERVER_ERROR`.

Do not document codes that are not emitted by source.

`SanitizingErrorFilter` converts errors carrying exceptions to the generic message “An internal error occurred.” and code `INTERNAL_SERVER_ERROR`. `GraphQL:IncludeExceptionDetails` controls development details and must remain false in production.

Mapper validation happens before create/update persistence. The current base does not translate database uniqueness or concurrency exceptions; clients receive a sanitized internal error for those failures.
