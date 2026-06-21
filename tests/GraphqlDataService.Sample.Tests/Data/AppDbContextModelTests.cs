using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Tests.Data;

public sealed class AppDbContextModelTests
{
    [Fact]
    public void Model_LoadsCustomerConfigurationFromProviderAssembly()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model;Username=test;Password=test")
            .Options;
        using var db = new AppDbContext(options, Options.Create(new DatabaseConfig
        {
            ProviderNamespacePrefix = "GraphqlDataService.Sample.Provider"
        }));

        var entity = db.Model.FindEntityType(typeof(CustomerEntity));

        Assert.NotNull(entity);
        Assert.Equal("customers", entity.GetTableName());
        Assert.Equal(DatabaseSchemas.Application, entity.GetSchema());
        Assert.Equal(320, entity.FindProperty(nameof(CustomerEntity.Email))?.GetMaxLength());
        Assert.Contains(entity.GetIndexes(), index =>
            index.IsUnique && index.Properties.Single().Name == nameof(CustomerEntity.Email));
    }
}
