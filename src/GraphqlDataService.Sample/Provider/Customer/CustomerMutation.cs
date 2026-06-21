using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL.Infrastructure;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

[ExtendObjectType(OperationTypeNames.Mutation)]
public sealed class CustomerMutation(CustomerMapper mapper) :
    CrudMutation<CustomerEntity, Guid, CustomerCreateInput, CustomerUpdateInput, CustomerMapper>(mapper)
{
    [GraphQLName("createCustomer")]
    public async Task<CustomerEntity> CreateCustomerAsync(
        CustomerCreateInput input,
        AppDbContext db,
        CancellationToken cancellationToken)
        => await CreateAsync(input, db, cancellationToken);

    [GraphQLName("updateCustomer")]
    public async Task<CustomerEntity> UpdateCustomerAsync(
        CustomerUpdateInput input,
        AppDbContext db,
        CancellationToken cancellationToken)
        => await UpdateAsync(input, db, cancellationToken);

    [GraphQLName("deleteCustomer")]
    public async Task<CustomerEntity> DeleteCustomerAsync(
        Guid id,
        AppDbContext db,
        CancellationToken cancellationToken)
        => await DeleteAsync(id, db, cancellationToken);
}
