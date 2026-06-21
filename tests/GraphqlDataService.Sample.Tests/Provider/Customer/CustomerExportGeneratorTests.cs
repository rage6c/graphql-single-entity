using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.Provider.Customer;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Tests.Provider.Customer;

public sealed class CustomerExportGeneratorTests
{
    [Fact]
    public void SupportedEntityName_UsesClrTypeNameInsteadOfAlias()
    {
        Assert.Equal("Customer", CustomerExportGenerator.SupportedEntityName);
    }

    [Fact]
    public void Generator_InheritsSharedCrudExportPipeline()
    {
        Assert.True(typeof(CustomerExportGenerator).IsSubclassOf(
            typeof(CrudExportGenerator<CustomerEntity>)));
    }
}
