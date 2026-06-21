# Logging And Observability

The service configures Serilog from configuration, enriches log context, writes to console, and writes rolling files under `logs/graphql-data-.log`. Files roll daily or at 10 MiB, with 50 retained files. `UseSerilogRequestLogging` records HTTP requests.

`ExportJobWorker` logs failed jobs with structured `ExportId` and `ServerName` properties. YAML records receive a generic error message; exception details remain in server logs.

Rules:

- Use structured placeholders, never interpolation, for operational logs.
- Do not log GraphQL variables, entity bodies, connection strings, YAML contents, or exported data.
- Log an unexpected exception once at the boundary that owns it.
- Add correlation IDs and OpenTelemetry before production; neither is implemented by the sample.
- Do not claim grid cache metrics: the current registry reads the database on every request.
