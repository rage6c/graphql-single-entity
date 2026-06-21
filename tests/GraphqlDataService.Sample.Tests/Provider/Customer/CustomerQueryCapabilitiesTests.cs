using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.Provider.Customer;
using HotChocolate;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

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

    [Fact]
    public void ApplyFilters_SupportsEveryCustomerFieldAndDateOperator()
    {
        var id = Guid.NewGuid();
        var rows = new[]
        {
            new CustomerEntity
            {
                Id = id,
                Name = "Ada",
                Email = "ada@example.test",
                BirthDate = new DateOnly(2000, 1, 2)
            },
            new CustomerEntity
            {
                Id = Guid.NewGuid(),
                Name = "Grace",
                Email = "grace@example.test",
                BirthDate = new DateOnly(2010, 1, 2)
            }
        }.AsQueryable();
        var capabilities = new CustomerQueryCapabilities();

        var filtered = capabilities.ApplyFilters(rows,
        [
            new() { Field = "id", Operator = ExportFilterOperator.Equal, Value = id.ToString("D") },
            new() { Field = "email", Operator = ExportFilterOperator.Contains, Value = "ada@" },
            new() { Field = "birthDate", Operator = ExportFilterOperator.GreaterThanOrEqual, Value = "2000-01-02" },
            new() { Field = "birthDate", Operator = ExportFilterOperator.LessThanOrEqual, Value = "2000-01-02" }
        ]).ToArray();

        Assert.Single(filtered);
        Assert.Equal("Ada", filtered[0].Name);
    }

    [Fact]
    public void ApplyOrderingAndFormatting_UsesStableIdAndInvariantValues()
    {
        var first = new CustomerEntity
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Same",
            Email = "b@example.test",
            BirthDate = null
        };
        var second = new CustomerEntity
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Name = "Same",
            Email = "a@example.test",
            BirthDate = new DateOnly(2000, 1, 2)
        };
        var capabilities = new CustomerQueryCapabilities();

        var ordered = capabilities.ApplyOrdering(
            new[] { second, first }.AsQueryable(),
            [new ExportOrderInput { Field = "name", Direction = ExportSortDirection.Ascending }])
            .ToArray();

        Assert.Equal(first.Id, ordered[0].Id);
        Assert.Equal("2000-01-02", capabilities.GetValue(second, "birthDate"));
        Assert.Equal(string.Empty, capabilities.GetValue(first, "birthDate"));
        Assert.Equal("Email", capabilities.GetPropertyName("email"));
    }

    [Theory]
    [InlineData("id", "invalid")]
    [InlineData("birthDate", "invalid")]
    public void ApplyFilters_InvalidTypedValue_Throws(string field, string value)
    {
        var capabilities = new CustomerQueryCapabilities();

        var exception = Assert.Throws<ExportGenerationException>(() =>
            capabilities.ApplyFilters(
                Array.Empty<CustomerEntity>().AsQueryable(),
                [new ExportFilterInput
                {
                    Field = field,
                    Operator = ExportFilterOperator.Equal,
                    Value = value
                }]).ToArray());

        Assert.Equal("EXPORT_FILTER_INVALID", exception.Code);
    }
}
