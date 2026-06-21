using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Infrastructure;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

[ExtendObjectType(OperationTypeNames.Query)]
public sealed class CustomerQuery : CrudQuery<CustomerEntity>
{
    [GraphQLName("customers")]
    [UsePaging(IncludeTotalCount = true)]
    [UseProjection]
    [UseFiltering(typeof(CustomerFilterType))]
    [UseSorting(typeof(CustomerSortType))]
    public IQueryable<CustomerEntity> GetCustomers(AppDbContext db) =>
        GetEntities(db);

    [GraphQLName("downloadCustomers")]
    public Task<ExportJob> DownloadCustomersAsync(
        ExportFileFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<ExportFilterInput>? filters,
        IReadOnlyList<ExportOrderInput>? order,
        [Service] IEntityExportJobService<CustomerEntity> jobs,
        CancellationToken cancellationToken) =>
        EnqueueExportAsync(format, columns, filters, order, jobs, cancellationToken);

    [GraphQLName("downloadCustomersByGridView")]
    public Task<ExportJob> DownloadCustomersByGridViewAsync(
        string gridViewName,
        [Service] IEntityExportJobService<CustomerEntity> jobs,
        CancellationToken cancellationToken) =>
        EnqueueGridViewExportAsync(gridViewName, jobs, cancellationToken);
}
