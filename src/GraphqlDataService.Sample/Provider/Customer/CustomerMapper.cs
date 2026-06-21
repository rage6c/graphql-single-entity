using GraphqlDataService.Sample.GraphQL.Infrastructure;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

public sealed class CustomerMapper :
    ICrudMapper<CustomerEntity, Guid, CustomerCreateInput, CustomerUpdateInput>
{
    public CustomerEntity Create(
        CustomerCreateInput input,
        DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = input.Name.Trim(),
            Email = input.Email.Trim(),
            BirthDate = input.BirthDate,
            CreatedAt = now,
            UpdatedAt = now
        };

    public void ApplyUpdate(
        CustomerEntity customer,
        CustomerUpdateInput input,
        DateTimeOffset now)
    {
        if (input.Name.HasValue)
        {
            customer.Name = input.Name.Value.Trim();
        }

        if (input.Email.HasValue)
        {
            customer.Email = input.Email.Value.Trim();
        }

        if (input.BirthDate.HasValue)
        {
            customer.BirthDate = input.BirthDate.Value;
        }

        customer.UpdatedAt = now;
    }

    public void Validate(CustomerEntity customer)
    {
        if (string.IsNullOrWhiteSpace(customer.Name) ||
            string.IsNullOrWhiteSpace(customer.Email))
        {
            throw new GraphQLException(ErrorBuilder.New()
                .SetMessage("Name and email are required.")
                .SetCode("VALIDATION_FAILED")
                .Build());
        }
    }

    public Guid GetId(CustomerUpdateInput input) => input.Id;
}
