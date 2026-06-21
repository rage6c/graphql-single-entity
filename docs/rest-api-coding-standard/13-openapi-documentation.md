# OpenAPI / API Documentation

Use OpenAPI to document API endpoints, request schemas, response schemas, response codes, and operational metadata. Generate OpenAPI documents from code to keep them in sync with implementation.

## Service Registration

Register OpenAPI services using the `Microsoft.AspNetCore.OpenApi` package.

```csharp
builder.Services.AddOpenApi();
```

Enable the OpenAPI endpoint in development:

```csharp
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

Rules:

- Enable the OpenAPI endpoint only in non-production environments by default.
- Use `app.MapOpenApi()` which serves the OpenAPI document at `/openapi/{documentName}.json`.
- Serve a Swagger UI in development only when the project intentionally uses an OpenAPI library that provides one.
- Do not expose the OpenAPI endpoint in production unless the API is internal and the security risk is accepted.

## Document Customization

Customize the OpenAPI document name, version, title, and description.

```csharp
builder.Services.AddOpenApi(options =>
{
    options.OpenApiVersion = OpenApiSpecVersion.OpenApi3_0;
    options.ShouldInclude = description =>
    {
        // Filter to only include controllers from a specific namespace
        return description.ActionDescriptor
            .EndpointMetadata
            .Any(m => m is ApiControllerAttribute);
    };
});
```

For more detailed customization, use an `IDocumentTransformer` or `IOperationTransformer`:

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Info = new()
        {
            Title = "Product API",
            Version = "v1",
            Description = "API for managing products."
        };
        return Task.CompletedTask;
    });
});
```

Rules:

- Set a meaningful title and version on the OpenAPI document.
- Use document transformers for cross-cutting metadata such as contact info, license, or external docs.
- Use operation transformers for endpoint-specific metadata that applies to many endpoints.

## Operation Metadata

Annotate endpoints with metadata using attributes or Minimal API extension methods.

```csharp
[HttpGet("{id:int}")]
[EndpointSummary("Get product by ID")]
[EndpointDescription("Returns a single product with full details.")]
public async Task<ActionResult<ProductDto>> GetByIdAsync(
    [FromRoute] int id,
    CancellationToken cancellationToken)
{
    var product = await productService.GetByIdAsync(id, cancellationToken);
    return Ok(product);
}
```

Rules:

- Add `[EndpointSummary]` and `[EndpointDescription]` to non-obvious endpoints.
- Keep summaries short (under 100 characters).
- Add descriptions for endpoints with complex behavior, non-standard status codes, or side effects.
- Do not add redundant descriptions that repeat the endpoint name or route.

## Schema Naming And Customization

Control how types appear in the generated OpenAPI schema.

```csharp
builder.Services.AddOpenApi(options =>
{
    options.AddSchemaTransformer((schema, context, cancellationToken) =>
    {
        if (context.JsonTypeInfo.Type == typeof(ProductDto))
        {
            schema.Description = "A product in the catalog.";
        }
        return Task.CompletedTask;
    });
});
```

Rules:

- Use schema transformers to add descriptions to DTOs when the default schema is unclear.
- Prefer data annotations on DTOs (`[Description("...")]`) over schema transformers when the description belongs on the DTO itself.
- Avoid adding every DTO as a separate schema entry. Use `[JsonIgnore]` or `[ReadOnly(true)]` to exclude internal fields.

## API Versioning With OpenAPI

When versioning is enabled, use `AddVersionedApiExplorer` to generate separate OpenAPI documents per version.

```csharp
builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

For versioning strategy and setup, see [12-api-versioning.md](12-api-versioning.md).

## Package Choice

Use `Microsoft.AspNetCore.OpenApi` (included with the ASP.NET Core framework) as the default OpenAPI library. It provides `AddOpenApi()`, `MapOpenApi()`, document transformers, operation transformers, and schema transformers.

For projects that require an interactive Swagger UI, `Swashbuckle.AspNetCore` or `NSwag` may be used instead. Evaluate the trade-offs:

| Library | Benefits | Drawbacks |
| --- | --- | --- |
| `Microsoft.AspNetCore.OpenApi` | Built-in, no extra package, transformers API | No built-in Swagger UI, fewer extension points |
| `Swashbuckle.AspNetCore` | Mature, Swagger UI built-in, widely documented | Heavier, separate versioning setup |
| `NSwag` | Code generation, Swagger UI, client generation | Complex configuration, heavier dependency |

Rules:

- Use `Microsoft.AspNetCore.OpenApi` for new projects.
- Use `Swashbuckle.AspNetCore` or `NSwag` only when the project requires Swagger UI or client code generation that the built-in library does not support.
- Do not mix OpenAPI libraries in the same project.

## Review Checklist

For the OpenAPI-related review checklist, see [17-review-checklist-examples.md](17-review-checklist-examples.md).
