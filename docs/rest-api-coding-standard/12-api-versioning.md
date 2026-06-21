# API Versioning

Versioning lets clients opt into API changes without breaking existing consumers. Choose a single strategy and apply it consistently.

## Versioning Strategy Options

| Strategy | Example | Pros | Cons |
| --- | --- | --- | --- |
| URL path | `/api/v1/products` | Explicit, cache-friendly, easy to route | URL changes are breaking per version |
| Query string | `/api/products?api-version=1.0` | Same URL for all versions | Easy to forget, not cache-friendly |
| Header | `X-Api-Version: 1.0` | Clean URLs, good for internal APIs | Hidden from documentation, harder to discover |
| Media type | `Accept: application/vnd.myapi.v1+json` | RESTful, content negotiation | Complex client setup, poor tooling support |

## Recommended: URL Path Versioning

Use URL path versioning for public and external-facing APIs. It makes the version explicit in every request and works well with caching, routing, and documentation.

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
}
```

For internal or service-to-service APIs, header-based versioning may be acceptable when URL cleanliness is preferred.

## Implementation With Asp.Versioning.Mvc

Use the `Asp.Versioning.Mvc` package (or `Asp.Versioning.Mvc.ApiExplorer` for OpenAPI integration).

```csharp
using Asp.Versioning;

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
});
```

Rules:

- Set `AssumeDefaultVersionWhenUnspecified` to true so unversioned requests default to the latest stable version.
- Set `ReportApiVersions` to true so clients receive `api-supported-versions` and `api-deprecated-versions` response headers.
- Use `UrlSegmentApiVersionReader` for URL path versioning.
- Map versions explicitly on controllers:

```csharp
[ApiVersion(1.0)]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDto>> GetByIdAsync(int id) { ... }

    [MapToApiVersion(2.0)]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductV2Dto>> GetByIdV2Async(int id) { ... }
}
```

## Deprecating And Sunsetting Versions

Mark old versions as deprecated and give clients time to migrate.

```csharp
[ApiVersion(1.0, Deprecated = true)]
[Route("api/v{version:apiVersion}/[controller]")]
public class ProductsController : ControllerBase
{
}
```

Rules:

- Set `Deprecated = true` on the `[ApiVersion]` attribute for deprecated versions.
- Communicate the sunset date in release notes, API documentation, and deprecation headers.
- Remove deprecated versions only after the announced sunset date has passed.
- Monitor usage of deprecated versions before removing them.
- Do not remove a version without a documented migration path to the replacement version.

## Versioning And OpenAPI

When using OpenAPI with versioning, register `ApiExplorer` to generate separate documents per version.

```csharp
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

For OpenAPI document configuration per version, see [13-openapi-documentation.md](13-openapi-documentation.md).

## Rules Summary

- Choose one versioning strategy and use it across all endpoints.
- Default unversioned requests to the latest stable API version.
- Report supported and deprecated versions in response headers.
- Mark deprecated versions early and communicate the sunset date.
- Remove old versions only after the announced sunset date.
- Do not use versioning to avoid designing stable contracts. Versioning is for necessary evolution.
- Do not expose internal implementation versions as API versions.
- Do not version internal service-to-service APIs unless they have external consumers.
