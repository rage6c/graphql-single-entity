using GraphqlDataService.Sample.Provider.Customer;
using HotChocolate;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Tests.Provider.Customer;

public sealed class CustomerMapperTests
{
    [Fact]
    public void Create_MapsInputAndServerOwnedFields()
    {
        var now = DateTimeOffset.UtcNow;
        var mapper = new CustomerMapper();
        var input = new CustomerCreateInput(
            "  Customer One  ",
            "  customer.one@example.test  ",
            new DateOnly(1990, 1, 1));

        var customer = mapper.Create(input, now);

        Assert.NotEqual(Guid.Empty, customer.Id);
        Assert.Equal("Customer One", customer.Name);
        Assert.Equal("customer.one@example.test", customer.Email);
        Assert.Equal(input.BirthDate, customer.BirthDate);
        Assert.Equal(now, customer.CreatedAt);
        Assert.Equal(now, customer.UpdatedAt);
    }

    [Fact]
    public void ApplyUpdate_PreservesOmittedFieldsAndClearsExplicitNull()
    {
        var originalEmail = "original@example.test";
        var now = DateTimeOffset.UtcNow;
        var mapper = new CustomerMapper();
        var customer = new CustomerEntity
        {
            Id = Guid.NewGuid(),
            Name = "Original",
            Email = originalEmail,
            BirthDate = new DateOnly(1990, 1, 1)
        };
        var input = new CustomerUpdateInput(
            customer.Id,
            new Optional<string>("  Updated  "),
            default,
            new Optional<DateOnly?>(null));

        mapper.ApplyUpdate(customer, input, now);

        Assert.Equal("Updated", customer.Name);
        Assert.Equal(originalEmail, customer.Email);
        Assert.Null(customer.BirthDate);
        Assert.Equal(now, customer.UpdatedAt);
    }

    [Fact]
    public void Validate_WithMissingName_ReturnsStableErrorCode()
    {
        var mapper = new CustomerMapper();
        var customer = new CustomerEntity
        {
            Name = string.Empty,
            Email = "customer@example.test"
        };

        var exception = Assert.Throws<GraphQLException>(() =>
            mapper.Validate(customer));

        Assert.Equal("VALIDATION_FAILED", Assert.Single(exception.Errors).Code);
    }
}
