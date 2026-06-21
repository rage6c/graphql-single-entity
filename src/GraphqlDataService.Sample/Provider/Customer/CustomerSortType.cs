using HotChocolate.Data.Sorting;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

public sealed class CustomerSortType : SortInputType<CustomerEntity>
{
    protected override void Configure(ISortInputTypeDescriptor<CustomerEntity> descriptor)
        => new CustomerQueryCapabilities().ConfigureSorting(descriptor);
}
