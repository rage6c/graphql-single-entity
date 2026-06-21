using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Infrastructure;
using Microsoft.Extensions.Options;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

[ExtendObjectType(OperationTypeNames.Subscription)]
public sealed class CustomerSubscription : CrudSubscription<CustomerEntity>
{
    public IAsyncEnumerable<ExportJobStatusUpdate> SubscribeToDownloadCustomersStatus(
        Guid exportId,
        [Service] IExportJobStore store,
        [Service] IOptions<ExportConfig> options,
        CancellationToken cancellationToken) =>
        SubscribeToExportStatusAsync(exportId, store, options, cancellationToken);

    [Subscribe(With = nameof(SubscribeToDownloadCustomersStatus))]
    [GraphQLName("downloadCustomersStatus")]
    public ExportJobStatusUpdate DownloadCustomersStatus(
        [EventMessage] ExportJobStatusUpdate update) => update;
}
