# CRUD Mutations

Concrete mutations are root type extensions and thin wrappers around the key-generic base:

```csharp
[ExtendObjectType(OperationTypeNames.Mutation)]
public sealed class CustomerMutation(CustomerMapper mapper) :
    CrudMutation<Customer, Guid, CustomerCreateInput, CustomerUpdateInput, CustomerMapper>(mapper)
{
    [GraphQLName("deleteCustomer")]
    public Task<Customer> DeleteCustomerAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken) =>
        DeleteAsync(id, db, cancellationToken);
}
```

`ICrudMapper<TEntity,TKey,TCreateInput,TUpdateInput>` owns input-to-entity mapping, update application, validation, and extraction of the update key. The base owns EF lookup, persistence, hard delete, and stable `NOT_FOUND` errors.

Rules:

- `TKey` may be `Guid`, `int`, `long`, or another non-null EF-supported single key type.
- Composite keys require a dedicated mutation implementation.
- Create inputs exclude generated keys and audit properties.
- Update inputs use Hot Chocolate `Optional<T>` so omission differs from explicit null.
- Standard mutations call `SaveChangesAsync` once and propagate cancellation.
- Delete calls `DbSet.Remove` and returns the removed entity. There is no soft-delete marker or hidden-row predicate.
- Mapper validation runs before create/update persistence.
- `EntityBase` is an empty marker; concrete entities declare keys and audit properties.

Database uniqueness and concurrency exceptions are not yet translated by the generic base. Add stable mappings before treating those errors as a production contract.
