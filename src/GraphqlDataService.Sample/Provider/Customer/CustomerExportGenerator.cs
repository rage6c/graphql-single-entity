using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL.Export;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

public sealed class CustomerExportGenerator(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IExportJobStore store,
    IEntityExportQueryCapabilities<CustomerEntity> queryCapabilities,
    IOptions<ExportConfig> options) :
    CrudExportGenerator<CustomerEntity>(dbContextFactory, store, queryCapabilities, options)
{
    public static string SupportedEntityName => typeof(CustomerEntity).Name;

    protected override string FileNamePrefix => "customers";
    protected override string WorksheetName => "Customers";
}
