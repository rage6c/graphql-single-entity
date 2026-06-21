namespace GraphqlDataService.Sample.GraphQL.Grid;

public interface IEntityGridDefinitionRegistry
{
    Task<GridDefinition> GetRequiredAsync(
        string entityName,
        string gridViewName,
        CancellationToken cancellationToken);
}
