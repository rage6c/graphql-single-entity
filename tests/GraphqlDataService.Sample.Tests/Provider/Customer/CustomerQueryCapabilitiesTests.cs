using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.Provider.Customer;
using HotChocolate;

namespace GraphqlDataService.Sample.Tests.Provider.Customer;

public sealed class CustomerQueryCapabilitiesTests
{
    [Fact]
    public void Validate_WithSupportedCriteria_DoesNotThrow()
    {
        var capabilities = new CustomerQueryCapabilities();
        var filters = new[]
        {
            new ExportFilterInput
            {
                Field = "name",
                Operator = ExportFilterOperator.Contains,
                Value = "Customer"
            }
        };
        var order = new[]
        {
            new ExportOrderInput
            {
                Field = "email",
                Direction = ExportSortDirection.Ascending
            }
        };

        capabilities.Validate(filters, order);
    }

    [Fact]
    public void Validate_WithUnsupportedOperator_ReturnsStableErrorCode()
    {
        var capabilities = new CustomerQueryCapabilities();
        var filters = new[]
        {
            new ExportFilterInput
            {
                Field = "id",
                Operator = ExportFilterOperator.Contains,
                Value = Guid.NewGuid().ToString("D")
            }
        };

        var exception = Assert.Throws<GraphQLException>(() => capabilities.Validate(filters, []));

        Assert.Equal("EXPORT_FILTER_INVALID", Assert.Single(exception.Errors).Code);
    }
}
