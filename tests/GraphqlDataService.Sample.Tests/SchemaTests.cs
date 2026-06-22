using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL;
using HotChocolate.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GraphqlDataService.Sample.Tests;

public sealed class SchemaTests
{
    [Fact]
    public async Task Schema_ContainsAllRequiredCustomerOperations()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseSqlServer(
                "Server=localhost;Database=schema;User Id=test;Password=test;TrustServerCertificate=true"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["GraphQL:ProviderNamespacePrefix"] =
                    "GraphqlDataService.Sample.Provider.Customer"
            })
            .Build();
        services.AddApplicationGraphQl(configuration);

        await using var provider = services.BuildServiceProvider();
        var executor = await provider.GetRequiredService<IRequestExecutorResolver>()
            .GetRequestExecutorAsync(cancellationToken: TestContext.Current.CancellationToken);
        var schema = executor.Schema.ToString();

        Assert.Contains("customers(", schema);
        Assert.Contains("totalCount", schema);
        Assert.Contains("downloadCustomers(", schema);
        Assert.Contains("filters:", schema);
        Assert.Contains("order:", schema);
        Assert.Contains("downloadCustomersByGridView(", schema);
        Assert.Contains("gridDefinition(", schema);
        Assert.Contains("createCustomer(", schema);
        Assert.Contains("updateCustomer(", schema);
        Assert.Contains("deleteCustomer(", schema);
        Assert.Contains("downloadCustomersStatus(", schema);
    }
}
