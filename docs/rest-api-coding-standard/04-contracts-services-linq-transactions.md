# Contracts, Services, LINQ, And Transactions

## Data Contracts and DTOs

Use DTOs to define API contracts. Do not expose EF entities as API contracts.

```csharp
public record CreateProductRequest(
    [Required, StringLength(120)] string Name,
    [StringLength(500)] string? Description,
    [Range(0.01, 999999.99)] decimal UnitPrice,
    [Range(0, int.MaxValue)] int StockQuantity);
```

DTO rules:

- Use request DTOs for input, such as `CreateProductRequest` and `UpdateProductRequest`.
- Use response DTOs for output, such as `ProductDto`.
- Prefer immutable `record` types for DTOs.
- Add data annotation validation for simple request validation.
- Keep DTOs free of business logic.
- Never include sensitive internal fields in response DTOs.

## Service Contracts

Define service contracts in `Services/Contracts`.

```csharp
public interface IProductService
{
    Task<IReadOnlyList<ProductDto>> GetAllAsync();
    Task<ProductDto> GetByIdAsync(int id);
    Task<ProductDto> CreateAsync(CreateProductRequest request);
    Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request);
    Task DeleteAsync(int id);
}
```

Contract rules:

- Return DTOs, not EF entities.
- Use `Task` and async methods for I/O work.
- Keep contracts focused on use cases, not database operations.
- Avoid leaking EF-specific types from service interfaces.

## Business Services

Services contain business logic, transactional workflows, data access orchestration, and mapping.

```csharp
public class ProductService(AppDbContext dbContext) : IProductService
{
    public async Task<ProductDto> GetByIdAsync(int id)
    {
        var product = await dbContext.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(product => product.Id == id);

        return product is null
            ? throw new NotFoundException($"Product with id {id} was not found.")
            : ToDto(product);
    }
}
```

Service rules:

- Put business rules in services, not controllers.
- Use `AsNoTracking()` for read-only queries.
- Throw custom application exceptions for expected failures.
- Keep DTO mapping close to the service unless mapping becomes large enough to justify a mapper.
- Avoid returning `IQueryable` outside the service.
- Prefer one service method per use case.

## LINQ Expressions

Keep LINQ execution in the layer that owns the data source. Build expressions deliberately and call terminal operations only at the correct boundary.

### LINQ Against EF Entities

LINQ against EF entities uses `IQueryable<T>` and is translated to SQL by EF Core.

```csharp
var products = await dbContext.Products
    .AsNoTracking()
    .Where(product => product.StockQuantity > 0)
    .OrderBy(product => product.Name)
    .Select(product => new ProductDto(
        product.Id,
        product.Name,
        product.Description,
        product.UnitPrice,
        product.StockQuantity,
        product.CreatedUtc,
        product.UpdatedUtc))
    .ToListAsync(cancellationToken);
```

EF LINQ rules:

- Use EF LINQ only in the data or service layer.
- Do not expose `IQueryable<TEntity>` from service contracts or controllers.
- Keep `Where`, `OrderBy`, `Skip`, `Take`, and `Select` before terminal operations.
- Prefer projecting to DTOs before materializing when the endpoint does not need full entities.
- Use async EF terminal operations such as `ToListAsync`, `FirstOrDefaultAsync`, `SingleOrDefaultAsync`, `AnyAsync`, and `CountAsync`.
- Pass `CancellationToken` to EF terminal operations when available.
- Use `AsNoTracking()` for read-only entity queries.
- Do not call `ToList`, `AsEnumerable`, or `ToArray` early just to continue filtering in memory.
- Do not use local C# methods inside EF expressions unless they are known to translate to SQL.
- Use `Include` only when returning entity graphs is required. Prefer `Select` projections for API DTOs.

### WHERE IN For LINQ-To-SQL Queries

Use `Contains` over a bounded, materialized scalar collection when the intended SQL shape is `WHERE ... IN (...)`.

```csharp
var productIds = request.ProductIds
    .Where(id => id > 0)
    .Distinct()
    .ToArray();

if (productIds.Length == 0)
{
    return [];
}

var products = await dbContext.Products
    .AsNoTracking()
    .Where(product => productIds.Contains(product.Id))
    .OrderBy(product => product.Name)
    .Select(product => new ProductDto(
        product.Id,
        product.Name,
        product.Description,
        product.UnitPrice,
        product.StockQuantity,
        product.CreatedUtc,
        product.UpdatedUtc))
    .ToListAsync(cancellationToken);
```

Bad:

```csharp
var products = await dbContext.Products
    .AsNoTracking()
    .Where(product => request.ProductIds.Any(id => id == product.Id))
    .ToListAsync(cancellationToken);
```

`WHERE IN` rules:

- Use `ids.Contains(entity.Id)` for scalar ID or code lookups that should translate to SQL `IN`.
- Materialize, normalize, validate, and de-duplicate the lookup values before building the EF query.
- Return an empty result before querying when the lookup list is empty.
- Keep lookup lists bounded. For large lists, use a provider-appropriate approach such as a temporary table, table-valued parameter, staging table, or bulk join pattern.
- Do not use entity objects in `Contains`; use scalar keys such as `int`, `Guid`, or `string`.
- Do not use local helper methods inside the `Contains` predicate unless the provider can translate them.
- Be aware of database parameter limits and query plan impact when sending many values.
- Keep `Contains`, `Where`, and `Select` before the terminal operation.

### LINQ Against Enumerable Objects

LINQ against `IEnumerable<T>` or in-memory collections runs in application memory after data has already been materialized.

```csharp
var expensiveProductNames = products
    .Where(product => product.UnitPrice >= 100)
    .OrderBy(product => product.Name)
    .Select(product => product.Name)
    .ToArray();
```

Enumerable LINQ rules:

- Use enumerable LINQ only after the data is intentionally materialized.
- Use enumerable LINQ in services or focused helper methods, not controllers.
- Do not fetch entire database tables just to filter with enumerable LINQ.
- Avoid repeated enumeration of expensive sequences. Materialize once when needed.
- Use `ToArray`, `ToList`, `ToDictionary`, `Any`, `All`, `Count`, `First`, and `Single` only when the sequence size and source are understood.
- Use `foreach` instead of LINQ when the operation has side effects.
- Keep LINQ expressions readable. Split complex expressions into named intermediate variables.
- Prefer database-side filtering for large datasets and in-memory LINQ for small materialized collections or business calculations.

### Predicates And Selectors

A predicate is an expression used to filter or test items, commonly passed to `Where`, `Any`, `All`, `FirstOrDefault`, or `SingleOrDefault`.

A selector is an expression used to project one shape into another, commonly passed to `Select`, `SelectMany`, `GroupBy`, or `ToDictionary`.

Use expression predicates and selectors for EF queries so EF Core can translate them to SQL.

```csharp
Expression<Func<Product, bool>> inStockPredicate =
    product => product.StockQuantity > 0;

Expression<Func<Product, ProductDto>> productSelector =
    product => new ProductDto(
        product.Id,
        product.Name,
        product.Description,
        product.UnitPrice,
        product.StockQuantity,
        product.CreatedUtc,
        product.UpdatedUtc);

var products = await dbContext.Products
    .AsNoTracking()
    .Where(inStockPredicate)
    .OrderBy(product => product.Name)
    .Select(productSelector)
    .ToListAsync(cancellationToken);
```

Use delegates for in-memory collections only after materialization is intentional.

```csharp
Func<ProductDto, bool> highValuePredicate =
    product => product.UnitPrice >= 100;

Func<ProductDto, string> nameSelector =
    product => product.Name;

var highValueProductNames = products
    .Where(highValuePredicate)
    .Select(nameSelector)
    .ToArray();
```

Predicate rules:

- Name reusable predicates with a `Predicate` suffix, such as `inStockPredicate`.
- Keep predicates side-effect free.
- Keep EF predicates translatable to SQL.
- Use `Expression<Func<TEntity, bool>>` for reusable EF predicates.
- Use `Func<T, bool>` only for in-memory enumerable predicates.
- Do not call local helper methods inside EF predicates unless they are known to translate to SQL.
- Prefer composing predicates before terminal operations.

Selector rules:

- Name reusable selectors with a `Selector` suffix, such as `productSelector`.
- Use selectors to project EF entities to DTOs before materializing read responses.
- Keep selectors side-effect free.
- Use `Expression<Func<TEntity, TDto>>` for reusable EF selectors.
- Use `Func<TSource, TResult>` only for in-memory enumerable selectors.
- Do not expose selectors through controller contracts.
- Avoid selectors that return EF entities from API-facing services.
- Split large selectors into named expressions or mapper methods when readability suffers.

### Terminal Operations by Layer

Terminal operations, also called terminating actions, execute the query or enumerate the sequence.

| Source | Allowed Layer | Terminal Examples | Rule |
| --- | --- | --- | --- |
| EF `IQueryable<TEntity>` | Data or service layer | `ToListAsync`, `FirstOrDefaultAsync`, `AnyAsync`, `CountAsync` | Execute before returning from the service. |
| DTO projection `IQueryable<TDto>` | Data or service layer | `ToListAsync`, `FirstOrDefaultAsync` | Project and execute before returning a normal DTO response. |
| Intentional stream | Service layer | `AsAsyncEnumerable` | Return only for documented streaming endpoints. |
| In-memory `IEnumerable<T>` | Service or helper layer | `ToList`, `ToArray`, `ToDictionary`, `Any`, `Count` | Use only after materialization is intentional. |
| Controller action | Avoid | Any EF terminal operation | Controllers should delegate query execution to services. |

## Transactions

Use explicit transactions for multi-step updates or workflows that must succeed or fail as one unit.

```csharp
public async Task<ProductDto> UpdateAsync(int id, UpdateProductRequest request)
{
    await using var transaction = await dbContext.Database.BeginTransactionAsync();

    var product = await dbContext.Products.FirstOrDefaultAsync(product => product.Id == id);
    if (product is null)
    {
        throw new NotFoundException($"Product with id {id} was not found.");
    }

    product.Name = request.Name.Trim();
    product.UnitPrice = request.UnitPrice;
    product.StockQuantity = request.StockQuantity;
    product.UpdatedUtc = DateTime.UtcNow;

    await dbContext.SaveChangesAsync();
    await transaction.CommitAsync();

    return ToDto(product);
}
```

Transaction rules:

- Start the transaction in the service layer.
- Keep the transaction scope small.
- Commit only after all required changes succeed.
- Do not swallow exceptions. Let failed transactions roll back on disposal.
- Do not use a transaction for simple single `SaveChangesAsync` calls unless business consistency requires it.
