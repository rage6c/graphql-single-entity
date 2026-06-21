using HotChocolate.Data.Filters;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

public sealed class CustomerFilterType : FilterInputType<CustomerEntity>
{
    protected override void Configure(IFilterInputTypeDescriptor<CustomerEntity> descriptor)
        => new CustomerQueryCapabilities().ConfigureFiltering(descriptor);
}
