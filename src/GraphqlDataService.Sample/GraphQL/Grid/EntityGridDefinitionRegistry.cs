using System.Text.Json;
using GraphqlDataService.Sample.Data;
using Microsoft.EntityFrameworkCore;

namespace GraphqlDataService.Sample.GraphQL.Grid;

public sealed class EntityGridDefinitionRegistry(
    IDbContextFactory<AppDbContext> dbContextFactory) : IEntityGridDefinitionRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<GridDefinition> GetRequiredAsync(
        string entityName,
        string gridViewName,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var row = await db.GridSchemas
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.IsActive &&
                    x.EntityName.ToLower() == entityName.ToLower() &&
                    x.ViewName.ToLower() == gridViewName.ToLower(),
                cancellationToken);

        if (row is null)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage($"Grid definition '{entityName}/{gridViewName}' was not found.")
                .SetCode("GRID_DEFINITION_NOT_FOUND")
                .Build());
        }

        try
        {
            return JsonSerializer.Deserialize<GridDefinition>(row.Definition, JsonOptions)
                ?? throw new JsonException("Grid definition is null.");
        }
        catch (JsonException)
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("The grid definition is invalid.")
                .SetCode("GRID_DEFINITION_INVALID")
                .Build());
        }
    }
}
