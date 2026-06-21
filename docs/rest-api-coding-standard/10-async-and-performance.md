# Async And Performance

## Async Method Signatures

Use async methods for all I/O-bound operations.

```csharp
public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken)
{
    var product = await dbContext.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    // ...
}
```

Rules:

- Use `Task<T>` for async methods that return a value.
- Use `Task` for async methods that do not return a value.
- Use `ValueTask<T>` only when the method frequently completes synchronously (e.g., a cache that often has a hit).
- End async method names with the `Async` suffix.
- Do not use `async void` except for event handlers.
- Do not block async calls with `.Result` or `.Wait()`.

Async rules:

- Use async EF Core methods such as `ToListAsync`, `FirstOrDefaultAsync`, and `SaveChangesAsync`.
- Avoid loading entire tables when filtering can be done in the database.
- Project to DTOs in queries when practical.
- Use async outbound client APIs for REST, GraphQL, gRPC, and SDK integrations.

## CancellationToken Propagation

Propagate `CancellationToken` through the entire async call chain from controller to database.

```csharp
[HttpGet("{id:int}")]
public async Task<ActionResult<ProductDto>> GetByIdAsync(
    [FromRoute] int id,
    CancellationToken cancellationToken)
{
    var product = await productService.GetByIdAsync(id, cancellationToken);
    return Ok(product);
}
```

ASP.NET Core automatically provides `HttpContext.RequestAborted` as a `CancellationToken` to controller actions.

Rules:

- Accept `CancellationToken` as the last parameter in async methods that perform I/O.
- Pass `CancellationToken` to EF Core terminal operations (`ToListAsync`, `FirstOrDefaultAsync`, `SaveChangesAsync`).
- Pass `CancellationToken` to HTTP client calls and integration client methods.
- Use `CancellationToken.None` only when cancellation must not occur (e.g., cleanup operations during shutdown).
- Do not create `CancellationTokenSource` per request. Use the token from `HttpContext.RequestAborted`.

## Avoiding Synchronous Blocking

Never block on async code. Synchronous blocking causes thread pool starvation and degrades performance under load.

Bad — blocking:

```csharp
var product = productService.GetByIdAsync(1).Result;      // Bad
var product = productService.GetByIdAsync(1).GetAwaiter().GetResult();  // Also bad
var product = Task.Run(() => productService.GetByIdAsync(1)).Result;    // Also bad
```

Good — await:

```csharp
var product = await productService.GetByIdAsync(1);
```

Rules:

- Never use `.Result`, `.Wait()`, or `.GetAwaiter().GetResult()` to block on async code.
- Never use `Task.Run` to synchronously block on async code.
- Never use `Thread.Sleep` in async methods. Use `Task.Delay`.
- If a method must be synchronous, keep it synchronous entirely. Do not mix sync wrappers around async code.

## Database Performance

Optimize database access to reduce latency and resource usage.

Rules:

- Use `AsNoTracking()` for read-only queries where change tracking is not needed.
- Use `Select` to project to DTOs before materializing to avoid fetching unused columns.
- Apply `Where`, `OrderBy`, `Skip`, and `Take` filters before materializing with `ToListAsync`.
- Use `FindAsync` for single-entity lookups by primary key when change tracking is needed.
- Avoid the N+1 query problem by using `Include` for related data that will be accessed.
- Prefer `AnyAsync` over `CountAsync` when only existence is being checked.
- Configure database connection pooling appropriately for the deployment environment.

## Response Compression

Enable response compression for text-based responses in deployed environments.

```csharp
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// ...
app.UseResponseCompression();
```

Rules:

- Enable compression for text-based content types (JSON, XML, HTML).
- Do not compress binary content (images, archives, videos) since they are already compressed.
- Use Brotli compression when the load balancer or client supports it.
- Disable compression for endpoints that stream large responses.
- Test compression ratio and CPU impact before enabling in production.

## Large Payload Handling

Handle large payloads without buffering everything in memory.

Rules:

- Use streaming (`IAsyncEnumerable<T>` or `FileStreamResult`) for responses that return large datasets.
- Use `[RequestSizeLimit]` to set explicit limits on request body sizes.
- Stream file uploads to disk or blob storage instead of buffering in memory.
- Avoid serializing large object graphs into a single JSON response.
- Use pagination for list endpoints instead of unbounded result sets.
- Set `Kestrel` limits (`MaxRequestBodySize`, `MaxRequestBufferSize`) when the platform requires non-default values.
