# Health Checks And Background Services

## Health Checks

Health check endpoints let orchestration platforms, load balancers, and monitoring systems verify application health.

### Health Check Endpoints

Register health check endpoints for liveness and readiness probes. For middleware ordering and registration in the composition root, see [09-program-cs.md](09-program-cs.md).

```csharp
builder.Services.AddHealthChecks();
// ...
app.MapHealthChecks("/health");
```

Separate liveness from readiness for Kubernetes-style probes:

```csharp
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // Liveness: no dependency checks
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")  // Readiness: includes dependency checks
});
```

Rules:

- Register a liveness endpoint (`/health/live`) that checks only process health.
- Register a readiness endpoint (`/health/ready`) that checks database, cache, and downstream dependency connectivity.
- Do not add expensive checks to the liveness endpoint.
- Use health checks in container orchestration probes, load balancer health probes, and monitoring dashboards.

### Common Health Check Implementations

Use the `AspNetCore.HealthChecks` ecosystem packages for common dependencies.

```csharp
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>(tags: ["ready"])
    .AddRedis(redisConnectionString, tags: ["ready"]);
```

Custom health check for an external dependency:

```csharp
public class CatalogApiHealthCheck(IHttpClientFactory httpClientFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient("CatalogClient");

        try
        {
            var response = await client.GetAsync("/health", cancellationToken);

            return response.IsSuccessStatusCode
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("Catalog API is unhealthy.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("Catalog API is unreachable.", exception);
        }
    }
}
```

Rules:

- Add health checks for every critical external dependency.
- Use the `AspNetCore.HealthChecks.*` packages where available.
- Implement `IHealthCheck` for custom or internal dependency checks.
- Use meaningful tags such as `"ready"` to group health checks.
- Set short timeouts on health check HTTP calls and database probes.
- Do not perform heavy computation or long-running I/O inside a health check.

### Health Check Response Customization

Write a custom `HealthCheckOptions` response writer for structured output.

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var result = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description
            })
        };
        await context.Response.WriteAsJsonAsync(result);
    }
});
```

### Startup Validation

Use `HostedService` or health checks to delay traffic until dependencies are ready.

```csharp
using var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

if (!await dbContext.Database.CanConnectAsync())
{
    throw new InvalidOperationException("Database is not reachable at startup.");
}
```

Rules:

- Validate critical dependency connectivity during startup.
- Fail fast when a required dependency is unavailable at startup.
- Do not use startup validation as a substitute for health checks. Both serve different purposes.

## Background Services

Use background services for recurring work, queue processing, scheduled maintenance, and long-running operations that should not block request processing.

### IHostedService vs BackgroundService

Use `BackgroundService` for most recurring or long-running background work. It provides a structured `ExecuteAsync` pattern with startup and shutdown lifecycle.

```csharp
public class ProductSyncService(IServiceScopeFactory scopeFactory, ILogger<ProductSyncService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Product sync service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<IProductSyncService>();
                await syncService.SyncAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Product sync failed.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
```

Implement `IHostedService` directly only when you need fine-grained control over start and stop ordering.

Rules:

- Prefer `BackgroundService` over `IHostedService` for recurring tasks.
- Use `IHostedService` only when `StartAsync` and `StopAsync` need separate, custom behavior.
- Register background services with `AddHostedService<T>`.

### Scoped Dependencies In Background Services

Background services are singletons. Use `IServiceScopeFactory` to resolve scoped dependencies inside the background loop.

```csharp
using var scope = scopeFactory.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
```

Rules:

- Always create a scope for scoped or transient dependencies.
- Create a new scope per iteration, not per application lifetime.
- Dispose scopes explicitly with `using` to release resources promptly.
- Do not inject scoped services directly into the background service constructor.

### Graceful Shutdown

Handle `CancellationToken` to stop gracefully on application shutdown.

```csharp
protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
    while (!stoppingToken.IsCancellationRequested)
    {
        await Task.Delay(interval, stoppingToken);
    }
}
```

Rules:

- Propagate `stoppingToken` to all async calls inside the background loop.
- Do not ignore `OperationCanceledException` during shutdown. Break the loop and exit cleanly.
- Dispose `IServiceScope`, `HttpClient`, and other resources when the service stops.
- Do not restart a background service that stopped due to cancellation unless the application explicitly manages restart logic.

### Timed Background Services

Use `PeriodicTimer` for precise interval-based execution in .NET 6+.

```csharp
public class PeriodicReportService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var reportService = scope.ServiceProvider.GetRequiredService<IReportService>();
            await reportService.GenerateReportAsync(stoppingToken);
        }
    }
}
```

Rules:

- Use `PeriodicTimer` for fixed-interval tasks where precise timing matters.
- Use `Task.Delay` for simple recurring loops where sub-second precision is not required.
- Do not overlap iterations. If a task takes longer than the interval, either skip the next tick or allow concurrency explicitly.

### Error Handling In Background Services

Unhandled exceptions in `ExecuteAsync` stop the background service. Always handle exceptions inside the loop.

```csharp
try
{
    await work.ExecuteAsync(stoppingToken);
}
catch (OperationCanceledException)
{
    break;
}
catch (TransientException exception) when (retryCount < maxRetries)
{
    retryCount++;
    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retryCount)), stoppingToken);
}
catch (Exception exception)
{
    logger.LogError(exception, "Fatal error in background service.");
    break;  // Stop the service on unexpected errors
}
```

Rules:

- Log all exceptions inside the background loop.
- Use retry policies for transient failures.
- Stop the service and let the orchestration platform restart it on persistent failures.
- Do not suppress exceptions silently.

### Background Service Testing

For testing background services, see [15-unit-testing-xunit.md](15-unit-testing-xunit.md).

## Review Checklist

For the health check and background service review checklist, see [17-review-checklist-examples.md](17-review-checklist-examples.md).
