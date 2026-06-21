using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.Tests;

internal static class TestDb
{
    public static AppDbContext Create(string? name = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options, Options.Create(new DatabaseConfig
        {
            ProviderNamespacePrefix = "GraphqlDataService.Sample.Provider"
        }));
    }

    public static IDbContextFactory<AppDbContext> Factory(string? name = null) =>
        new FactoryImpl(name ?? Guid.NewGuid().ToString("N"));

    private sealed class FactoryImpl(string name) : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => Create(name);
    }
}
