using GraphqlDataService.Sample.Data.Entities;

namespace GraphqlDataService.Sample.GraphQL.Infrastructure;

public interface ICrudMapper<TEntity, TKey, in TCreateInput, in TUpdateInput>
    where TEntity : EntityBase
{
    TEntity Create(TCreateInput input, DateTimeOffset now);
    void ApplyUpdate(TEntity entity, TUpdateInput input, DateTimeOffset now);
    void Validate(TEntity entity);
    TKey GetId(TUpdateInput input);
}
