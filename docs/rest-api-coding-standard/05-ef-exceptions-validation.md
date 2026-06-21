# EF Core, Exceptions, And Validation

## EF Core Data Layer

Use Fluent API for EF Core mapping. Keep entity classes as persistence POCOs without EF mapping annotations. Every EF model must define a default schema and every mapped table must explicitly declare its table name and schema.

```csharp
public class Product
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public decimal UnitPrice { get; set; }
}
```

Configure entities with Fluent API:

```csharp
public static class DatabaseSchemas
{
    public const string Catalog = "catalog";
}

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Catalog);

        modelBuilder.Entity<Product>(entity =>
        {
            entity.ToTable("Products", DatabaseSchemas.Catalog);
            entity.HasKey(product => product.Id);

            entity.Property(product => product.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(product => product.Description)
                .HasMaxLength(500);

            entity.Property(product => product.UnitPrice)
                .HasPrecision(18, 2);

            entity.HasIndex(product => product.Name)
                .IsUnique();
        });
    }
}
```

EF Core rules:

- Configure keys, required fields, max lengths, precision, indexes, relationships, conversions, query filters, and table mapping with Fluent API.
- Do not use EF mapping annotations such as `[Key]`, `[Required]`, `[StringLength]`, `[Precision]`, `[Index]`, `[Table]`, or `[Column]` on entity classes.
- Define a mandatory default schema with `modelBuilder.HasDefaultSchema(...)` for every `DbContext`.
- Define a mandatory schema for each mapped table with `entity.ToTable("TableName", schemaName)`, even when the table uses the default schema.
- Keep schema names in constants, such as `DatabaseSchemas.Catalog`, instead of repeating string literals.
- Do not rely on implicit provider defaults such as `dbo` or `public`; choose the schema explicitly.
- Keep `DbContext` focused on `DbSet<TEntity>` properties and model configuration.
- Move mapping into `IEntityTypeConfiguration<TEntity>` classes when `OnModelCreating` becomes noisy, and register them with `ApplyConfigurationsFromAssembly`.
- Use `DbSet<TEntity>` only in the data and service layers.
- Prefer `AsNoTracking()` for reads.
- Use `Include` only when related data is needed.
- Avoid lazy loading unless the project explicitly standardizes on it.
- Use migrations for production systems. `EnsureCreatedAsync` is acceptable only for samples and demos.
- Use UTC timestamps for persisted dates.

## Exception Handling

Use centralized exception handling middleware that returns `ProblemDetails`.

```csharp
var (statusCode, title, detail) = exception switch
{
    NotFoundException => (StatusCodes.Status404NotFound, "Resource not found", exception.Message),
    ConflictException => (StatusCodes.Status409Conflict, "Request conflict", exception.Message),
    _ => (StatusCodes.Status500InternalServerError, "Unexpected server error", "An unexpected error occurred.")
};
```

Exception rules:

- Use custom exceptions for expected application failures.
- Return `404` for missing resources.
- Return `409` for conflicts such as duplicate unique values.
- Return `400` for model validation errors.
- Return `500` only for unexpected failures.
- Log unexpected exceptions as errors.
- Do not expose stack traces or database internals in API responses.

## Validation

Use a layered validation approach.

- Use data annotations on request DTOs for simple field validation.
- Use service-level validation for rules that require database access.
- Trim user-facing string values before persistence.
- Reject invalid IDs and invalid numeric ranges.
- Use meaningful validation messages where possible.
