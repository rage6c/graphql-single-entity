# Configuration

Load configuration into strongly typed objects. Register validated configuration objects in dependency injection as singletons.

## Configuration Object Pattern

Use a dedicated class for each configuration section.

```csharp
using System.ComponentModel.DataAnnotations;

public class CatalogClientConfig
{
    public const string SectionName = "CatalogClient";

    [Required]
    [Url]
    public string BaseUrl { get; init; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 10;
}
```

Rules:

- Use one configuration object per section.
- Use the suffix `Config` for configuration objects.
- Include a `SectionName` constant.
- Use `init` setters where practical.
- Add data annotation validation for required values and safe ranges.
- Do not inject `IConfiguration` throughout application code.
- Do not read raw configuration values inside controllers, services, or integration clients.

## appsettings Example

```json
{
  "CatalogClient": {
    "BaseUrl": "https://catalog.internal",
    "TimeoutSeconds": 10
  }
}
```

Rules:

- Keep non-secret defaults in `appsettings.json`.
- Keep environment-specific values in environment-specific configuration or platform configuration.
- Store secrets in secret stores, environment variables, or managed identity-based configuration.
- Do not commit passwords, API keys, client secrets, private keys, certificates, or secret tokens.

## Environment Configuration

Use environment-specific configuration for values that differ by deployment environment.

Recommended environments:

| Environment | Purpose | Example Environment Name |
| --- | --- | --- |
| Development | Local developer machine | `Development` |
| SIT | System integration testing | `SIT` |
| UAT | User acceptance testing | `UAT` |
| DR | Disaster recovery environment | `DR` |
| Production | Live production environment | `Production` |

Recommended files:

```text
appsettings.json
appsettings.Development.json
appsettings.SIT.json
appsettings.UAT.json
appsettings.DR.json
appsettings.Production.json
```

Rules:

- Keep shared non-secret defaults in `appsettings.json`.
- Keep local developer overrides in `appsettings.Development.json`.
- Keep SIT values pointed at integration-test dependencies.
- Keep UAT values production-like, but isolated from production data and services.
- Keep DR values aligned with disaster recovery infrastructure and failover dependencies.
- Keep production values minimal, explicit, and reviewed.
- Use environment variables, platform configuration, or secret stores for secrets in every environment.
- Do not use production secrets in Development, SIT, or UAT.
- Do not point non-production environments at production databases, queues, storage accounts, or downstream APIs.
- Validate required configuration during startup in every environment.
- Document any environment-specific feature flags and their intended lifecycle.

Suggested override order:

```text
appsettings.json
appsettings.{Environment}.json
Environment variables
Secret store or platform-injected configuration
Command-line arguments
```

Example:

```json
{
  "CatalogClient": {
    "BaseUrl": "https://catalog-sit.internal",
    "TimeoutSeconds": 10
  },
  "FeatureFlags": {
    "EnableCatalogFallback": true
  }
}
```

Environment review checklist:

- Environment name is explicit and matches deployment naming.
- Non-production environments do not connect to production resources.
- Secrets are supplied outside committed JSON files.
- DR configuration has been tested with failover dependencies.
- UAT configuration mirrors production behavior without production data exposure.
- Feature flags are documented and reviewed before production release.

## Singleton Registration

Bind, validate, and register the configuration object as a singleton.

```csharp
var catalogClientConfig = builder.Configuration
    .GetRequiredSection(CatalogClientConfig.SectionName)
    .Get<CatalogClientConfig>()
    ?? throw new InvalidOperationException("CatalogClient configuration is required.");

Validator.ValidateObject(
    catalogClientConfig,
    new ValidationContext(catalogClientConfig),
    validateAllProperties: true);

builder.Services.AddSingleton(catalogClientConfig);
```

Usage:

```csharp
public class CatalogRestClient(
    HttpClient httpClient,
    CatalogClientConfig config)
{
    public Task<CatalogItemDto?> GetItemAsync(
        int id,
        CancellationToken cancellationToken)
    {
        httpClient.BaseAddress = new Uri(config.BaseUrl);
        httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);

        return GetItemCoreAsync(id, cancellationToken);
    }
}
```

Rules:

- Register configuration objects with `AddSingleton`.
- Validate configuration during startup.
- Fail fast when required configuration is missing or invalid.
- Inject the typed configuration object into services and clients.
- Do not inject `IConfiguration` into business services.
- Do not mutate configuration singleton instances at runtime.

## Options Pattern (IOptions, IOptionsSnapshot, IOptionsMonitor)

Use the options interfaces when configuration needs to be reloadable or consumed through DI conventions.

| Interface | Lifetime | Reloads | Use Case |
| --- | --- | --- | --- |
| `IOptions<T>` | Singleton | No | Static config that does not change at runtime |
| `IOptionsSnapshot<T>` | Scoped | Per request | Config that needs to reload on each request |
| `IOptionsMonitor<T>` | Singleton | Yes, on change | Config that must be monitored for runtime changes |

```csharp
builder.Services.Configure<CatalogClientConfig>(
    builder.Configuration.GetRequiredSection(CatalogClientConfig.SectionName));

public class CatalogRestClient(IOptions<CatalogClientConfig> options) { }
```

Rules:

- Use `IOptions<T>` for configuration that is read once and does not change at runtime.
- Use `IOptionsSnapshot<T>` when configuration changes between requests must be picked up immediately.
- Use `IOptionsMonitor<T>` for singletons that need to react to configuration changes (e.g., background services).
- Prefer binding and registering concrete config objects as singletons (see Singleton Registration section) for most application configuration. Use `IOptions<T>` primarily for framework-integrated configuration.
- Do not inject `IOptions<T>` everywhere. Inject the concrete typed config object directly when the config is static.

## Configuration Validation At Startup

Validate configuration at startup and fail fast when required values are missing or invalid.

```csharp
var config = builder.Configuration
    .GetRequiredSection(CatalogClientConfig.SectionName)
    .Get<CatalogClientConfig>()
    ?? throw new InvalidOperationException("CatalogClient configuration is required.");

var validationResults = new List<ValidationResult>();
var context = new ValidationContext(config);

if (!Validator.TryValidateObject(config, context, validationResults, validateAllProperties: true))
{
    var errors = string.Join(", ", validationResults.Select(v => v.ErrorMessage));
    throw new InvalidOperationException($"CatalogClient configuration is invalid: {errors}");
}
```

For the options pattern, implement `IValidateOptions<T>`:

```csharp
public class CatalogClientConfigValidation : IValidateOptions<CatalogClientConfig>
{
    public ValidateOptionsResult Validate(string? name, CatalogClientConfig config)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(config.BaseUrl))
            failures.Add("BaseUrl is required.");

        if (config.TimeoutSeconds < 1 || config.TimeoutSeconds > 300)
            failures.Add("TimeoutSeconds must be between 1 and 300.");

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(string.Join("; ", failures));
    }
}

builder.Services.AddSingleton<IValidateOptions<CatalogClientConfig>, CatalogClientConfigValidation>();
```

Rules:

- Always validate configuration at startup, not on first use.
- Throw descriptive exceptions that identify which configuration section is invalid.
- Use data annotations for simple validation (required, range, URL format).
- Use `IValidateOptions<T>` for complex validation rules that span multiple properties or require custom logic.
- Fail the application startup when required configuration is missing or invalid.

## Good And Bad Examples

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

Good:

```csharp
builder.Services.AddSingleton(productRulesConfig);
```

Bad:

```csharp
builder.Services.AddScoped(_ =>
    builder.Configuration.GetSection("ProductRules").Get<ProductRulesConfig>());
```

## Review Checklist

For the configuration-related review checklist, see [17-review-checklist-examples.md](17-review-checklist-examples.md).
