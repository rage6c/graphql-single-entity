using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.Data.Entities;

namespace GraphqlDataService.Sample.GraphQL.Infrastructure;

public abstract class CrudMutation<TEntity, TKey, TCreateInput, TUpdateInput, TMapper>(TMapper mapper)
    where TEntity : EntityBase
    where TKey : notnull
    where TMapper : ICrudMapper<TEntity, TKey, TCreateInput, TUpdateInput>
{
    protected async Task<TEntity> CreateAsync(
        TCreateInput input,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = mapper.Create(input, DateTimeOffset.UtcNow);
        mapper.Validate(entity);
        db.Set<TEntity>().Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    protected async Task<TEntity> UpdateAsync(
        TUpdateInput input,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await RequiredAsync(db, mapper.GetId(input), cancellationToken);
        mapper.ApplyUpdate(entity, input, DateTimeOffset.UtcNow);
        mapper.Validate(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    protected async Task<TEntity> DeleteAsync(
        TKey id,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var entity = await RequiredAsync(db, id, cancellationToken);
        db.Set<TEntity>().Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return entity;
    }

    private static async Task<TEntity> RequiredAsync(
        AppDbContext db,
        TKey id,
        CancellationToken cancellationToken) =>
        await db.Set<TEntity>().FindAsync([id], cancellationToken)
        ?? throw new GraphQLException(ErrorBuilder.New()
            .SetMessage($"{typeof(TEntity).Name} was not found.")
            .SetCode("NOT_FOUND")
            .Build());
}
