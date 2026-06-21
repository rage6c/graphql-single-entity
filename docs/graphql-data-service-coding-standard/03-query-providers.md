# Query Providers

Each entity query is a root type extension derived from `CrudQuery<TEntity>`:

```csharp
[ExtendObjectType(OperationTypeNames.Query)]
public sealed class CustomerQuery : CrudQuery<Customer>
{
    [GraphQLName("customers")]
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering(typeof(CustomerFilterType))]
    [UseSorting(typeof(CustomerSortType))]
    public IQueryable<Customer> GetCustomers(AppDbContext db) => GetEntities(db);
}
```

Middleware order is paging, projection, filtering, then sorting. Entity filter and sort types bind fields explicitly through `EntityExportQueryCapabilities<TEntity>` so GraphQL and exports share the same field definitions.

`CrudQuery<TEntity>`:

- calls `db.Set<TEntity>().AsNoTracking()`;
- returns `IQueryable<TEntity>` without materializing it;
- provides helpers for explicit-column and named-grid-view export jobs;
- adds no implicit ordering or soft-delete predicate.

Cursor paging accepts `first`/`after` and `last`/`before`. Clients obtain `totalCount`, `nodes` or `edges`, and `pageInfo`; a cursor is opaque and does not expose a page number. To navigate directly to page three, the client must retain the preceding cursor or use a separately designed offset-paging field.

The configured defaults are 25 rows and a maximum of 100 rows in `appsettings.json`.

Every entity query exposes:

- `download{EntityPlural}(format, columns, filters, order)`;
- `download{EntityPlural}ByGridView(gridViewName)`.

Both return job metadata, not file bytes.
