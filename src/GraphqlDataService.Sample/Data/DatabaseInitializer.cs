using System.Text.Json;
using GraphqlDataService.Sample.Data.Entities;
using GraphqlDataService.Sample.GraphQL.Grid;
using Microsoft.EntityFrameworkCore;

namespace GraphqlDataService.Sample.Data;

public sealed class DatabaseInitializer(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IEnumerable<IEntityGridDefinition> seeds)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);

        foreach (var seed in seeds)
        {
            var exists = await db.GridSchemas.AnyAsync(
                x => x.EntityName == seed.EntityName && x.ViewName == seed.GridViewName,
                cancellationToken);
            if (exists)
            {
                continue;
            }

            var definition = new GridDefinition(
                seed.EntityName,
                seed.GridViewName,
                seed.ExportFormat,
                seed.Columns);
            db.GridSchemas.Add(new GridSchema
            {
                Id = Guid.NewGuid(),
                EntityName = seed.EntityName,
                ViewName = seed.GridViewName,
                Definition = JsonSerializer.Serialize(definition),
                Version = 1,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                CreatedBy = "sample-seed"
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
