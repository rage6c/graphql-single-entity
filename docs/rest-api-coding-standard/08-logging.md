# Logging

Use structured logging.

```csharp
logger.LogError(exception, "Unhandled exception while processing {Method} {Path}.",
    context.Request.Method,
    context.Request.Path);
```

Logging rules:

- Write application logs to both console and files.
- Use console logs for local development and container or platform log collection.
- Roll log files daily.
- Roll log files by size with a default limit of `10 MB` per file.
- Keep log files under a dedicated `logs/` folder or configured platform log directory.
- Log unexpected failures at `Error`.
- Log handled application exceptions at `Warning` or lower.
- Do not log secrets, tokens, passwords, or full payment data.
- Use structured placeholders instead of string interpolation in log messages.

## Log Level Guidance

Use log levels consistently to make log filtering predictable.

| Level | When To Use | Example |
| --- | --- | --- |
| `Verbose` | Diagnostic data useful only for local debugging | `logger.LogVerbose("SQL: {Query}", query);` |
| `Debug` | Detailed information useful during development | `logger.LogDebug("Processing order {OrderId}.", order.Id);` |
| `Information` | Normal application flow, successful operations | `logger.LogInformation("Product {ProductId} created.", product.Id);` |
| `Warning` | Handled failures, unexpected but non-fatal conditions | `logger.LogWarning("Product {ProductId} not found.", id);` |
| `Error` | Unhandled exceptions, failures that affect a single operation | `logger.LogError(exception, "Failed to process order {OrderId}.", order.Id);` |
| `Critical` | Catastrophic failures that affect the entire application | `logger.LogCritical(exception, "Database connection pool exhausted.");` |

Rules:

- Log handled application exceptions at `Warning`, not `Error`.
- Log unexpected exceptions at `Error`.
- Log startup failures and data corruption at `Critical`.
- Do not log at `Verbose` or `Debug` in production unless troubleshooting a specific issue.
- Use structured placeholders (`{Property}`) instead of string interpolation at every level.

## Serilog Enrichers

Use enrichers to add consistent context to every log event.

```csharp
Log.Logger = new LoggerConfiguration()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithCorrelationId()
    .CreateLogger();
```

Register enrichers in `Program.cs`:

```csharp
builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithEnvironmentName());
```

Recommended enrichers:

| Enricher | Package | Purpose |
| --- | --- | --- |
| `FromLogContext` | Serilog | Include properties from `LogContext.PushProperty` |
| `WithMachineName` | Serilog.Enrichers.Environment | Identify the source machine |
| `WithEnvironmentName` | Serilog.Enrichers.Environment | Identify the deployment environment |
| `WithCorrelationId` | Serilog.Enrichers.CorrelationId | Trace requests across services |
| `WithThreadId` | Serilog.Enrichers.Thread | Debug concurrency issues |

Rules:

- Enrich with environment and machine name for all deployments.
- Add correlation ID enrichment for request tracing.
- Do not enrich with sensitive data such as user PII, tokens, or credentials.

## Structured Logging Best Practices

Use structured placeholders, not string interpolation.

Good:

```csharp
logger.LogInformation("User {UserId} purchased {Quantity} units of {ProductId}.",
    userId, quantity, productId);
```

Bad:

```csharp
logger.LogInformation($"User {userId} purchased {quantity} units of {productId}.");
```

Rules:

- Always use structured placeholders (`{PropertyName}`) in log templates.
- Use PascalCase for property names to match structured logging conventions.
- Use the `@` destructuring operator for complex objects: `logger.LogInformation("Order {@Order}", order);`
- Do not use string interpolation inside log method calls.

## Log Filtering And Sensitivity

Redact sensitive data before logging.

```csharp
logger.LogInformation("Processing payment for order {OrderId}.", order.Id);
// Do NOT log: logger.LogInformation("Processing payment {CreditCard}.", creditCardNumber);
```

Rules:

- Never log passwords, tokens, API keys, credit card numbers, personal identifiers, or full payment payloads.
- Never log file contents, binary data, or full HTTP request/response bodies.
- Mask or truncate sensitive fields in structured logs when the schema requires partial logging.
- Use Serilog filtering or custom enrichers to redact known sensitive properties at the sink level.

## Centralized Log Aggregation

For production systems, forward logs to a centralized platform.

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/api-.log", rollingInterval: Day)
    .WriteTo.Seq("http://seq.internal:5341")
    .CreateLogger();
```

Common log aggregation targets:

| Target | Package | Use Case |
| --- | --- | --- |
| Seq | Serilog.Sinks.Seq | Structured log search during development and staging |
| Elasticsearch | Serilog.Sinks.ElasticSearch | Full-text search, Kibana dashboards |
| Datadog | Serilog.Sinks.Datadog.Logs | Integrated APM and log monitoring |
| Application Insights | Serilog.Sinks.ApplicationInsights | Azure-centric monitoring |
| Splunk | Serilog.Sinks.Splunk | Enterprise log aggregation |

Rules:

- Forward logs to a centralized platform in staging and production.
- Use structured sinks (Seq, Elasticsearch, Datadog) that preserve property names and types.
- Keep console logging enabled for container and Kubernetes log collection.
- Do not send verbose or debug logs to the aggregation platform in production.

Recommended logging defaults:

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/api-.log",
          "rollingInterval": "Day",
          "rollOnFileSizeLimit": true,
          "fileSizeLimitBytes": 10485760,
          "retainedFileCountLimit": 31
        }
      }
    ]
  }
}
```
