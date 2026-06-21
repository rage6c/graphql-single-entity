# Controllers, Requests, And Responses

## Controllers

Controllers should be thin. They receive HTTP requests, delegate work to services, and return HTTP responses.

```csharp
[ApiController]
[Route("api/[controller]")]
public class ProductsController(IProductService productService) : ControllerBase
{
    [HttpGet("{id:int}", Name = "GetProductById")]
    public async Task<ActionResult<ProductDto>> GetByIdAsync(int id)
    {
        var product = await productService.GetByIdAsync(id);
        return Ok(product);
    }
}
```

Controller rules:

- Use `[ApiController]`.
- Use route constraints where useful, such as `{id:int}`.
- Return `ActionResult<T>` for endpoints with response bodies.
- Use `CreatedAtRoute` or `CreatedAtAction` for successful creates.
- Do not catch application exceptions in controllers. Let middleware handle them.
- Do not inject `DbContext` into controllers.
- Do not return EF entities directly.

Recommended response codes:

| Operation | Success | Common Errors |
| --- | --- | --- |
| GET collection | `200 OK` | `500` |
| GET by id | `200 OK` | `404` |
| POST | `201 Created` or `200 OK` | `400`, `409` |
| PUT | `200 OK` or `204 NoContent` | `400`, `404`, `409` |
| DELETE | `204 NoContent` | `404` |

## API Request Inputs

Use explicit binding sources for all non-trivial controller inputs.

### Query String

Use query strings for filtering, searching, sorting, paging, sparse field selection, and other optional read parameters.

```csharp
public record ProductSearchQuery(
    string? Search,
    int Page = 1,
    int PageSize = 25,
    string? SortBy = null);

[HttpGet]
public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetAllAsync(
    [FromQuery] ProductSearchQuery query,
    CancellationToken cancellationToken)
{
    var products = await productService.GetAllAsync(query, cancellationToken);
    return Ok(products);
}
```

Query string rules:

- Use `[FromQuery]` for complex query models.
- Use query DTOs for endpoints with more than two query parameters.
- Keep query parameters optional unless the endpoint naturally requires them.
- Apply safe defaults for paging, such as `Page = 1` and `PageSize = 25`.
- Enforce a maximum `PageSize`.
- Use stable names such as `page`, `pageSize`, `sortBy`, `sortDirection`, `search`, and `filter`.
- Do not send sensitive values such as passwords, tokens, or personal identifiers in query strings.
- Do not use query strings for large object graphs.

### Path Variables

Use path variables for resource identity and hierarchy.

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

Path variable rules:

- Use route constraints such as `{id:int}`, `{orderId:guid}`, or `{slug:regex(...)}` where appropriate.
- Use `[FromRoute]` when route binding is not immediately obvious.
- Keep route templates resource-oriented, not action-oriented.
- Do not put optional filters in the path. Use query strings for filters.
- Keep path variables stable because changing them is a breaking API change.
- Validate that path IDs match any duplicated body IDs, or avoid body IDs on update requests.

### Request Body

Use request bodies for create, update, command, and complex input payloads.

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

Request body rules:

- Use `[FromBody]` for JSON request DTOs when the binding source is not obvious.
- Use one request body DTO per endpoint.
- Do not bind EF entities from request bodies.
- Do not use request bodies for `GET` endpoints.
- Use `[Consumes("application/json")]` when an endpoint must accept only JSON.
- Keep request DTOs purpose-specific, such as `CreateProductRequest` and `UpdateProductRequest`.
- Do not trust client-provided IDs, timestamps, ownership fields, or security fields.
- Apply model validation and service-level validation before persistence.
- Set request size limits for large or risky endpoints.

### Multi-File Uploads

Use `multipart/form-data` for multi-file upload endpoints.

```csharp
public record UploadProductFilesRequest(
    [Required] int ProductId,
    [Required] IReadOnlyList<IFormFile> Files);

[HttpPost("{id:int}/files")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(50_000_000)]
public async Task<ActionResult<IReadOnlyList<FileUploadResultDto>>> UploadFilesAsync(
    [FromRoute] int id,
    [FromForm] UploadProductFilesRequest request,
    CancellationToken cancellationToken)
{
    var result = await productService.UploadFilesAsync(id, request, cancellationToken);
    return Ok(result);
}
```

Multi-file rules:

- Use `[FromForm]` with `IFormFile`, `IReadOnlyList<IFormFile>`, or a multipart request DTO.
- Use `[Consumes("multipart/form-data")]`.
- Set explicit request size limits with `[RequestSizeLimit]` or endpoint configuration.
- Validate file count, file size, file extension, and content type.
- Do not trust the client-provided file name. Generate server-side storage names.
- Stream files to storage when possible instead of buffering large files in memory.
- Scan uploaded files when the platform requires malware checks.
- Store file metadata separately from file content.
- Return per-file upload results for partial success scenarios.
- Do not log file contents or sensitive file names.

## API Responses

Choose the response shape based on payload size, client behavior, and transport requirements.

### DTO Responses

Use DTO responses for normal JSON APIs.

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

DTO response rules:

- Return response DTOs, not EF entities.
- Use `ActionResult<T>` for status-code-aware JSON responses.
- Use `IReadOnlyList<TDto>` for bounded collections.
- Use paged response DTOs for large collections.
- Include only fields the API contract intentionally exposes.
- Use consistent date/time formats and prefer UTC.
- Return `ProblemDetails` for errors.

### IAsyncEnumerable Responses

Use `IAsyncEnumerable<T>` only when the endpoint intentionally streams a large JSON sequence.

```csharp
[HttpGet("stream")]
public IAsyncEnumerable<ProductDto> StreamAsync(
    [FromQuery] ProductSearchQuery query,
    CancellationToken cancellationToken) =>
    productService.StreamAsync(query, cancellationToken);
```

`IAsyncEnumerable` rules:

- Return DTOs, not EF entities.
- Use for large read-only streams where paging is not a good fit.
- Prefer paging for ordinary list endpoints.
- Propagate `CancellationToken`.
- Use `AsNoTracking()` for EF-backed streams.
- Keep the data context alive for the duration of enumeration.
- Avoid doing long-running CPU work inside the async iterator.
- Document whether the response is a streamed JSON array, newline-delimited JSON, or another media type.
- Do not combine `IAsyncEnumerable` streaming with response shapes that require total counts unless the count is fetched separately.

### Direct Binary Streaming

Use direct binary streaming for downloads, exports, images, documents, archives, and other non-JSON content.

```csharp
[HttpGet("{id:int}/download")]
public async Task<IActionResult> DownloadAsync(
    [FromRoute] int id,
    CancellationToken cancellationToken)
{
    var file = await productService.OpenDownloadAsync(id, cancellationToken);
    return File(
        file.Stream,
        file.ContentType,
        file.FileName,
        enableRangeProcessing: true);
}
```

Direct binary streaming rules:

- Return `FileStreamResult`, `PhysicalFileResult`, `VirtualFileResult`, or `Results.Stream` for binary content.
- Do not load large binary files fully into memory.
- Set the correct `Content-Type`.
- Set a safe download file name when using `Content-Disposition`.
- Enable range processing for large downloadable files when supported.
- Validate file existence and return `404` without exposing internal storage paths.
- Do not expose local file system paths in responses.
- Do not log binary content.
- Dispose streams correctly. Transfer ownership clearly between service and framework response.
