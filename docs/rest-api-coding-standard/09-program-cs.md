# Program.cs

`Program.cs` is the application composition root. Keep it focused on startup wiring, configuration binding, dependency injection, middleware ordering, and endpoint mapping.

## Responsibilities

`Program.cs` may contain:

- Host and logging setup.
- Strongly typed configuration binding and validation.
- Dependency injection registration.
- Database provider registration.
- Middleware pipeline ordering.
- Controller, GraphQL, health check, and OpenAPI endpoint mapping.
- Startup-only database initialization for samples or demos.

`Program.cs` must not contain:

- Business logic.
- EF queries for request handling.
- DTO mapping logic.
- Integration client request logic.
- Long helper methods.
- Environment-specific branching that belongs in configuration.

## Recommended Shape

```csharp
using System.ComponentModel.DataAnnotations;
using MyApi.Configuration;
using MyApi.Data;
using MyApi.Middleware;
using MyApi.Services;
using MyApi.Services.Contracts;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

var databaseConfig = builder.Configuration
    .GetRequiredSection(DatabaseConfig.SectionName)
    .Get<DatabaseConfig>()
    ?? throw new InvalidOperationException("Database configuration is required.");

Validator.ValidateObject(
    databaseConfig,
    new ValidationContext(databaseConfig),
    validateAllProperties: true);

builder.Services.AddSingleton(databaseConfig);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(databaseConfig.ConnectionString));

builder.Services.AddScoped<IProductService, ProductService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

app.MapControllers();

app.Run();
```

## Dependency Registration Rules

- Register services against interfaces where interfaces exist.
- Register EF `DbContext` as scoped through `AddDbContext`.
- Register business services as scoped.
- Register validated configuration objects as singleton.
- Register typed HTTP clients through `AddHttpClient`.
- Keep DI registration readable and grouped by concern.
- Move large registration groups into extension methods when `Program.cs` becomes noisy.

For detailed DI registration rules, see [06-dependency-injection.md](06-dependency-injection.md).

Good:

```csharp
builder.Services.AddScoped<IProductService, ProductService>();
```

Bad:

```csharp
builder.Services.AddScoped<ProductService>();
```

## Configuration Rules

Use strongly typed configuration objects. See [02-configuration.md](02-configuration.md).

Good:

```csharp
var productRulesConfig = builder.Configuration
    .GetRequiredSection(ProductRulesConfig.SectionName)
    .Get<ProductRulesConfig>()
    ?? throw new InvalidOperationException("ProductRules configuration is required.");

builder.Services.AddSingleton(productRulesConfig);
```

Bad:

```csharp
builder.Services.AddSingleton(builder.Configuration);
```

Rules:

- Validate required configuration at startup.
- Fail fast when configuration is missing or invalid.
- Do not pass raw `IConfiguration` into controllers or business services.
- Do not hard-code environment-specific URLs, credentials, or feature flags.

## Middleware Ordering

Use a predictable middleware order.

Recommended order:

```csharp
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();
app.UseHttpsRedirection();

app.MapControllers();
```

For health check endpoint registration, see [14-health-checks-background-services.md](14-health-checks-background-services.md).

Rules:

- Register exception handling early.
- Register request logging near the start of the pipeline.
- Use HTTPS redirection in deployed environments.
- Map endpoints after middleware registration.
- Do not put business logic in middleware unless it is truly cross-cutting.

## Environment Rules

Use `app.Environment` only for startup behavior.

Allowed:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

Avoid:

```csharp
if (app.Environment.IsProduction())
{
    catalogUrl = "https://catalog.prod.internal";
}
```

Rules:

- Put environment-specific values in configuration, not code.
- Limit environment branching to startup features such as OpenAPI, diagnostics, or sample seed behavior.
- Do not branch business rules by environment in `Program.cs`.

## Database Startup Rules

For samples and demos, `EnsureCreatedAsync` is acceptable.

```csharp
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}
```

For production systems:

- Prefer migrations over `EnsureCreatedAsync`.
- Do not run destructive database operations from `Program.cs`.
- Keep startup database initialization short and explicit.
- Move complex initialization into a hosted service or deployment job.

## Extension Method Pattern

Use extension methods when `Program.cs` grows too large.

```csharp
builder.Services.AddApplicationServices();
builder.Services.AddPersistence(databaseConfig);
builder.Services.AddApiClients();
```

Rules:

- Extension methods should be grouped by concern.
- Extension methods should not hide surprising side effects.
- Keep names clear, such as `AddPersistence`, `AddApplicationServices`, and `AddApiClients`.
