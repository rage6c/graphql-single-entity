namespace GraphqlDataService.Sample.GraphQL.Grid;

[ExtendObjectType(OperationTypeNames.Query)]
public sealed class GridDefinitionQuery
{
    public Task<GridDefinition> GetGridDefinitionAsync(
        [Service] IEntityGridDefinitionRegistry registry,
        string entityName,
        string gridViewName = "default",
        CancellationToken cancellationToken = default) =>
        registry.GetRequiredAsync(entityName, gridViewName, cancellationToken);
}
