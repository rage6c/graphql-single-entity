# Integration Clients

Use integration clients for outbound calls to external or internal APIs such as REST, GraphQL, gRPC, SOAP, message gateways, and vendor SDKs.

Recommended structure:

```text
IntegrationClients/
  Contracts/              Client interfaces
  Rest/                   REST client implementations
  GraphQL/                GraphQL client implementations
  Grpc/                   gRPC client implementations
  Models/                 External request and response models
  Configuration/          Client configuration objects
```

Client contract example:

```csharp
public interface ICatalogClient
{
    Task<CatalogItemDto?> GetItemAsync(int id, CancellationToken cancellationToken);
}
```

REST client registration example:

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>((serviceProvider, client) =>
    {
        var config = serviceProvider.GetRequiredService<CatalogClientConfig>();

        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
    });
```

Integration client constraints:

- Do not call external APIs directly from controllers.
- Business services must depend on integration client interfaces, not concrete HTTP, GraphQL, or gRPC implementations.
- Use typed clients for REST integrations through `IHttpClientFactory`.
- Do not instantiate `HttpClient` manually with `new HttpClient()` in application code.
- Use generated or strongly typed clients for gRPC where possible.
- Keep GraphQL documents close to the GraphQL client and version them when the upstream schema changes.
- Keep external API request and response models separate from internal DTOs and EF entities.
- Map external models to internal DTOs or domain models inside the client or service boundary.
- Store base URLs, timeouts, scopes, and feature flags in typed configuration objects.
- Store secrets, API keys, client secrets, and certificates in secure configuration, not source control.
- Apply explicit timeouts to every outbound dependency.
- Propagate `CancellationToken` from controller to service to integration client where practical.
- Do not log tokens, API keys, cookies, or full sensitive payloads.
- Convert transport-specific failures into application-specific exceptions or result types.
- Handle expected downstream errors such as `404`, `409`, `429`, and `503` deliberately.
- Add retry policies only for transient and idempotent operations.
- Do not retry non-idempotent operations such as payment creation or order submission unless the API supports idempotency keys.
- Use circuit breakers or bulkheads for critical high-volume dependencies.
- Include correlation or trace IDs in outbound requests when supported.
- Keep integration clients small and focused on one upstream system.
- Do not leak `HttpResponseMessage`, gRPC response envelopes, or GraphQL transport details into controllers.

## Resilience Pipeline (Polly)

Use the `Microsoft.Extensions.Http.Resilience` package for standardized resilience policies on HTTP clients. It integrates with `IHttpClientFactory` and provides defaults for retry, circuit breaker, timeout, and bulkhead.

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>(client =>
    {
        client.BaseAddress = new Uri("https://catalog.internal");
        client.Timeout = TimeSpan.FromSeconds(10);
    })
    .AddStandardResilienceHandler();  // Default retry, circuit breaker, timeout, rate limiter
```

Customize the standard resilience handler for the specific downstream dependency:

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>(client =>
    {
        client.BaseAddress = new Uri("https://catalog.internal");
        client.Timeout = TimeSpan.FromSeconds(30);
    })
    .AddStandardResilienceHandler(options =>
    {
        options.Retry.MaxRetryAttempts = 3;
        options.Retry.Delay = TimeSpan.FromSeconds(1);
        options.Retry.MaxDelay = TimeSpan.FromSeconds(10);

        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
        options.CircuitBreaker.MinimumThroughput = 5;
    });
```

Rules:

- Use `AddStandardResilienceHandler()` for all HTTP clients that call external services.
- Configure retry delays based on the downstream service's rate-limiting and latency characteristics.
- Enable the circuit breaker for critical dependencies to fail fast when the downstream service is unhealthy.
- Set circuit breaker `BreakDuration` long enough for the downstream service to recover.
- Do not retry on `400 Bad Request` or `404 Not Found`. These are client errors, not transient failures.
- Do not retry non-idempotent operations such as payment creation or order submission unless the API supports idempotency keys.

## Correlation ID Propagation

Propagate correlation or trace IDs in outbound requests to enable end-to-end tracing across services.

```csharp
public class CorrelationIdDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdDelegatingHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _httpContextAccessor.HttpContext?
            .Request.Headers["X-Correlation-ID"].FirstOrDefault();

        if (correlationId is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```

Register the delegating handler:

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>(client => { ... })
    .AddHttpMessageHandler<CorrelationIdDelegatingHandler>();
```

Rules:

- Propagate the same correlation ID across all outbound calls within a single request scope.
- Use `DelegatingHandler` for cross-cutting HTTP concerns such as correlation IDs and tracing headers.
- Do not propagate internal correlation IDs to external or third-party APIs unless the contract specifies it.

## Client Certificate Transport

Use client certificates only when a downstream service contract explicitly requires mutual TLS.

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>(client => { ... })
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ClientCertificates.Add(clientCertificate);
        return handler;
    });
```

Rules:

- Load client certificates from secure configuration or certificate stores, not from source control.
- Use `X509Store` or Azure Key Vault / AWS Certificate Manager for production certificate management.
- Do not embed certificate passwords or PFX files in source code or configuration files.

Recommended outbound error mapping:

| Downstream Result | Local Handling |
| --- | --- |
| `400 Bad Request` | Treat as integration contract or caller validation failure |
| `404 Not Found` | Return null or throw a not-found style integration exception based on the use case |
| `409 Conflict` | Throw a conflict-style exception |
| `429 Too Many Requests` | Respect retry headers and fail fast when retry is unsafe |
| `5xx` | Treat as transient only when the operation is safe to retry |
| Timeout | Throw a timeout-specific integration exception |
