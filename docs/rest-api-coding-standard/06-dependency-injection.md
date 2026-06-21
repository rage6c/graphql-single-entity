# Dependency Injection

Register services and infrastructure in `Program.cs`.

For strongly typed configuration object rules, see [02-configuration.md](02-configuration.md).

```csharp
var databaseConfig = builder.Configuration
    .GetRequiredSection(DatabaseConfig.SectionName)
    .Get<DatabaseConfig>()
    ?? throw new InvalidOperationException("Database configuration is required.");

builder.Services.AddSingleton(databaseConfig);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(databaseConfig.ConnectionString));

builder.Services.AddScoped<IProductService, ProductService>();
```

DI rules:

- Register EF `DbContext` as scoped.
- Register business services as scoped.
- Depend on interfaces from controllers.
- Avoid service locator patterns such as manually resolving services outside composition code.

## Scoped vs Transient vs Singleton

Choose the DI lifetime based on the service's state and usage pattern.

| Lifetime | Instance Per | Use Case | Risk |
| --- | --- | --- | --- |
| Singleton | One instance for the entire application lifetime | Caching, configuration, logging, HTTP client factories | Captive dependencies — holding a scoped dependency in a singleton |
| Scoped | One instance per HTTP request | DbContext, business services, unit of work | Accidental singleton capture — injected into a singleton |
| Transient | A new instance every time it is resolved | Lightweight stateless helpers, converters | Overhead from excessive allocation |

Rules:

- Use scoped for services that depend on `DbContext`.
- Use singleton for stateless services and configuration objects.
- Use transient for lightweight, stateless utility services.
- Do not inject scoped services into singletons. This creates a captive dependency.
- Do not use transient for services that require per-request state.

## Captive Dependencies

A captive dependency occurs when a singleton or longer-lived service holds a reference to a shorter-lived service (scoped or transient). The shorter-lived service is resolved once and never released, defeating the purpose of the shorter lifetime.

Bad — singleton holding a scoped dependency:

```csharp
builder.Services.AddSingleton<IProductService, ProductService>();
// ProductService depends on AppDbContext (scoped) — captive!
```

Good — matching lifetimes:

```csharp
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<AppDbContext>();
```

Rules:

- Never inject scoped or transient services into singletons.
- Use DI analysis tools or analyzers to detect captive dependencies during development.
- When a background service needs scoped dependencies, use `IServiceScopeFactory` (see [14-health-checks-background-services.md](14-health-checks-background-services.md)).

## Keyed Services (.NET 8+)

Use keyed services when multiple implementations of the same interface are needed.

```csharp
builder.Services.AddKeyedScoped<IProductService, InMemoryProductService>("memory");
builder.Services.AddKeyedScoped<IProductService, DatabaseProductService>("database");

public class ProductController(
    [FromKeyedServices("database")] IProductService productService)
{
}
```

Rules:

- Use keyed services for strategy or provider patterns where the implementation varies by context.
- Use consistent string keys, preferably matching the implementation's purpose.
- Document keyed service keys so consumers know which key to use.
- Do not use keyed services when a single implementation is sufficient.

## Convention-Based Registration (Scrutor)

Use Scrutor for assembly-scanning registration when many services follow the same interface pattern.

```csharp
builder.Services.Scan(scan => scan
    .FromAssembliesOf<IProductService>()
    .AddClasses(classes => classes.AssignableTo<IProductService>())
    .AsImplementedInterfaces()
    .WithScopedLifetime());
```

Rules:

- Use Scrutor when the project has many services that all follow the `IService` / `Service` naming convention.
- Prefer explicit registration for individual services in small-to-medium projects.
- Keep Scrutor scans scoped to specific assemblies, not the entire application domain.

## Open Generic Registration

Register open generics when the same pattern applies across multiple types.

```csharp
builder.Services.AddTransient(typeof(ILogger<>), typeof(Logger<>));
```

Rules:

- Use open generics for cross-cutting infrastructure such as logging, caching, or telemetry.
- Do not use open generics for business services. Register business services explicitly.

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
