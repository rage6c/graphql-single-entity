# Review Checklist Examples

Before merging REST API code, verify the following patterns. Each section shows the right way followed by the wrong way.

## Project Structure And Naming

### Controllers are thin, services own business logic

Good:

```csharp
[HttpPost]
public async Task<ActionResult<ProductDto>> CreateAsync(
    [FromBody] CreateProductRequest request,
    CancellationToken cancellationToken)
{
    var product = await productService.CreateAsync(request, cancellationToken);
    return CreatedAtRoute("GetProductById", new { id = product.Id }, product);
}
```

Bad:

```csharp
[HttpPost]
public async Task<ActionResult<ProductDto>> CreateAsync(Product request)
{
    if (await dbContext.Products.AnyAsync(p => p.Name == request.Name))
        return Conflict();

    dbContext.Products.Add(request);
    await dbContext.SaveChangesAsync();
    return Ok(new ProductDto(request.Id, request.Name, null, request.UnitPrice, request.StockQuantity, request.CreatedUtc, null));
}
```

### DTOs are separate from EF entities

Good:

```csharp
public record CreateProductRequest(string Name, decimal UnitPrice, int StockQuantity);
public record ProductDto(int Id, string Name, decimal UnitPrice, int StockQuantity);
```

Bad:

```csharp
[HttpPost]
public async Task<ActionResult<Product>> CreateAsync([FromBody] Product product)
{
    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync();
    return product;
}
```

## Configuration

### Configuration uses typed objects, not raw IConfiguration

Good:

```csharp
public class ProductService(ProductRulesConfig config)
{
    public bool IsLowStock(int stockQuantity) =>
        stockQuantity <= config.LowStockThreshold;
}
```

Bad:

```csharp
public class ProductService(IConfiguration configuration)
{
    public bool IsLowStock(int stockQuantity)
    {
        var threshold = configuration.GetValue<int>("ProductRules:LowStockThreshold");
        return stockQuantity <= threshold;
    }
}
```

### Configuration is validated at startup

Good:

```csharp
var config = builder.Configuration
    .GetRequiredSection(CatalogClientConfig.SectionName)
    .Get<CatalogClientConfig>()
    ?? throw new InvalidOperationException("CatalogClient configuration is required.");

Validator.ValidateObject(config, new ValidationContext(config), validateAllProperties: true);
```

Bad:

```csharp
// Configuration is never validated — first request may fail with cryptic null reference
```

## Controllers, Requests, And Responses

### Service contracts return DTOs

Good:

```csharp
public interface IProductService
{
    Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken);
}
```

Bad:

```csharp
public interface IProductService
{
    IQueryable<Product> GetProducts();
    Task<Product> GetByIdAsync(int id);
}
```

### Business rules are in services

Good:

```csharp
public async Task<ProductDto> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
{
    var exists = await dbContext.Products
        .AnyAsync(p => p.Name == request.Name.Trim(), cancellationToken);

    if (exists)
        throw new ConflictException($"Product name '{request.Name}' is already in use.");

    var product = new Product { Name = request.Name.Trim(), UnitPrice = request.UnitPrice };
    dbContext.Products.Add(product);
    await dbContext.SaveChangesAsync(cancellationToken);

    return ToDto(product);
}
```

Bad:

```csharp
[HttpPost]
public async Task<IActionResult> CreateAsync(CreateProductRequest request)
{
    var exists = await dbContext.Products.AnyAsync(p => p.Name == request.Name);
    if (exists)
        return Conflict("Duplicate product name.");
    return Ok();
}
```

### Read queries use AsNoTracking where appropriate

Good:

```csharp
var products = await dbContext.Products
    .AsNoTracking()
    .OrderBy(p => p.Name)
    .ToListAsync(cancellationToken);
```

Bad:

```csharp
var products = await dbContext.Products
    .OrderBy(p => p.Name)
    .ToListAsync(cancellationToken);
```

### LINQ terminal operations are called in the owning layer

Good:

```csharp
public async Task<IReadOnlyList<ProductDto>> GetAllAsync(CancellationToken cancellationToken)
{
    return await dbContext.Products
        .AsNoTracking()
        .Select(p => ToDto(p))
        .ToListAsync(cancellationToken);
}
```

Bad:

```csharp
public IQueryable<Product> GetProducts() => dbContext.Products;

[HttpGet]
public async Task<ActionResult<IReadOnlyList<Product>>> GetAllAsync()
{
    return await productService.GetProducts().ToListAsync();
}
```

### Transactions are used for multi-step consistency

Good:

```csharp
await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
order.Status = OrderStatus.Paid;
payment.Status = PaymentStatus.Captured;
await dbContext.SaveChangesAsync(cancellationToken);
await transaction.CommitAsync(cancellationToken);
```

Bad:

```csharp
order.Status = OrderStatus.Paid;
await dbContext.SaveChangesAsync();
payment.Status = PaymentStatus.Captured;
await dbContext.SaveChangesAsync();
```

## EF Core, Exceptions, And Validation

### EF mapping uses Fluent API with explicit schemas

Good:

```csharp
modelBuilder.HasDefaultSchema(DatabaseSchemas.Catalog);

modelBuilder.Entity<Product>(entity =>
{
    entity.ToTable("Products", DatabaseSchemas.Catalog);
    entity.HasKey(product => product.Id);
    entity.Property(product => product.Name).IsRequired().HasMaxLength(120);
    entity.Property(product => product.UnitPrice).HasPrecision(18, 2);
    entity.HasIndex(product => product.Name).IsUnique();
});
```

Bad:

```csharp
[Table("Products")]
[Index(nameof(Name), IsUnique = true)]
public class Product
{
    [Key]
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;
}
```

### Exceptions return consistent ProblemDetails

Good:

```csharp
var problem = new ProblemDetails
{
    Status = StatusCodes.Status404NotFound,
    Title = "Resource not found",
    Detail = exception.Message,
    Instance = context.Request.Path
};
await context.Response.WriteAsJsonAsync(problem);
```

Bad:

```csharp
catch (Exception exception)
{
    return StatusCode(500, new { error = exception.ToString() });
}
```

### Input validation is present on DTOs

Good:

```csharp
public record CreateProductRequest(
    [Required, StringLength(120)] string Name,
    [Range(0.01, 999999.99)] decimal UnitPrice,
    [Range(0, int.MaxValue)] int StockQuantity);
```

Bad:

```csharp
public record CreateProductRequest(string Name, decimal UnitPrice, int StockQuantity);
```

## Dependency Injection

### Services are registered against interfaces

Good:

```csharp
builder.Services.AddScoped<IProductService, ProductService>();
```

Bad:

```csharp
builder.Services.AddScoped<ProductService>();
```

### No captive dependencies

Good:

```csharp
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<AppDbContext>();
```

Bad:

```csharp
builder.Services.AddSingleton<IProductService, ProductService>();
// ProductService depends on AppDbContext (scoped) — captive!
```

## Integration Clients

### Outbound integrations use typed clients with timeouts

Good:

```csharp
builder.Services
    .AddHttpClient<ICatalogClient, CatalogRestClient>((sp, client) =>
    {
        var config = sp.GetRequiredService<CatalogClientConfig>();
        client.BaseAddress = new Uri(config.BaseUrl);
        client.Timeout = TimeSpan.FromSeconds(10);
    });
```

Bad:

```csharp
public async Task<string> GetCatalogItemAsync(int id)
{
    using var client = new HttpClient();
    return await client.GetStringAsync($"https://catalog.internal/items/{id}?apiKey=secret");
}
```

## Logging

### Use structured placeholders, not string interpolation

Good:

```csharp
logger.LogInformation("User {UserId} purchased {Quantity} units of {ProductId}.", userId, quantity, productId);
```

Bad:

```csharp
logger.LogInformation($"User {userId} purchased {quantity} units of {productId}.");
```

## Program.cs

### Program.cs is a composition root only

Good:

```csharp
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddDbContext<AppDbContext>(...);
```

Bad:

```csharp
// Business logic or complex conditional registration in Program.cs
```

## Async And Performance

### No blocking on async code

Good:

```csharp
var product = await productService.GetByIdAsync(id, cancellationToken);
```

Bad:

```csharp
var product = productService.GetByIdAsync(id).Result;
var product = productService.GetByIdAsync(id).GetAwaiter().GetResult();
```

### CancellationToken is propagated through the call chain

Good:

```csharp
public async Task<ProductDto> GetByIdAsync(int id, CancellationToken cancellationToken)
{
    return await dbContext.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
}
```

Bad:

```csharp
public async Task<ProductDto> GetByIdAsync(int id)
{
    return await dbContext.Products
        .AsNoTracking()
        .FirstOrDefaultAsync(p => p.Id == id);
}
```

## Configuration

### No secrets are committed

Good:

```json
{
  "Database": { "ConnectionString": "${STORE_DB_CONNECTION_STRING}" },
  "CatalogClient": { "BaseUrl": "https://catalog.internal" }
}
```

Bad:

```json
{
  "Database": { "ConnectionString": "Server=prod-db;User Id=admin;Password=P@ssw0rd!" },
  "CatalogClient": { "ApiKey": "live-secret-api-key" }
}
```

## Formatting And Style

### Nullable reference types are enabled

Good:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
</PropertyGroup>
```

Bad:

```xml
<PropertyGroup>
  <Nullable>disable</Nullable>
</PropertyGroup>
```

## API Versioning

### Versioning strategy is applied consistently

Good:

```csharp
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase { }
```

Bad:

```csharp
// No versioning — all endpoints are implicitly v1 with no upgrade path
```

## OpenAPI

### OpenAPI endpoint is restricted in production

Good:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

Bad:

```csharp
app.MapOpenApi();  // Exposed in production with no restriction
```

## Health Checks And Background Services

### Liveness and readiness are separated

Good:

```csharp
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });
```

Bad:

```csharp
app.MapHealthChecks("/health");  // No distinction between liveness and readiness
```

### Background services handle graceful shutdown

Good:

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    await work.ExecuteAsync(stoppingToken);
    await Task.Delay(interval, stoppingToken);
}
```

Bad:

```csharp
while (true)
{
    work.Execute();
    Thread.Sleep(interval);
}
```

## Unit Testing

### Tests do not call real databases or HTTP APIs

Good:

```csharp
var productService = new Mock<IProductService>();
productService
    .Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
    .ReturnsAsync(product);
var controller = new ProductsController(productService.Object);
```

Bad:

```csharp
var dbContext = new AppDbContext(realOptions);
var controller = new ProductsController(dbContext);
```

## Package Checks

### No unapproved vulnerable packages

Good:

```text
dotnet list package --vulnerable --include-transitive
# No high or critical vulnerabilities found
```

Bad:

```text
High severity vulnerability found but merged without approval
```

### dotnet build passes with zero warnings where practical

Good:

```bash
dotnet build
```

Expected result:

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Bad:

```text
Build FAILED.
    6 Warning(s)
    1 Error(s)
```

## Summary Checklist

Before merging, verify all applicable items:

- [ ] Controllers delegate to services, do not contain business logic or EF queries.
- [ ] DTOs are separate from EF entities.
- [ ] Configuration is loaded into typed objects and validated at startup.
- [ ] `IConfiguration` is not injected into business services.
- [ ] Services are registered against interfaces with correct lifetimes.
- [ ] No captive dependencies (singletons holding scoped services).
- [ ] `AsNoTracking()` is used for read-only queries.
- [ ] LINQ terminal operations run in the owning layer.
- [ ] Transactions are used for multi-step consistency.
- [ ] EF mapping uses Fluent API, `HasDefaultSchema(...)`, and `ToTable(..., schema)` for every table.
- [ ] Exceptions return `ProblemDetails` via middleware.
- [ ] Input validation is present on request DTOs.
- [ ] Outbound integrations use typed clients with timeouts and resilience.
- [ ] `HttpClient` is not instantiated with `new HttpClient()` in application code.
- [ ] Structured placeholders are used instead of string interpolation in logs.
- [ ] Secrets, tokens, and sensitive data are not logged.
- [ ] `.Result`, `.Wait()`, and `.GetAwaiter().GetResult()` are not present.
- [ ] `CancellationToken` is propagated through async call chains.
- [ ] Secrets are not committed in configuration files.
- [ ] Nullable reference types are enabled.
- [ ] API versioning strategy is consistent.
- [ ] OpenAPI endpoint is restricted in production.
- [ ] Health checks separate liveness from readiness.
- [ ] Background services handle graceful shutdown via `CancellationToken`.
- [ ] Unit tests mock interfaces and do not call real databases or HTTP APIs.
- [ ] Package vulnerability checks pass with no unapproved high or critical vulnerabilities.
- [ ] `dotnet build` passes with zero warnings.
