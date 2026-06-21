using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Grid;
using GraphqlDataService.Sample.Provider.Customer;
using HotChocolate;
using Microsoft.Extensions.Options;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;
using IOPath = System.IO.Path;

namespace GraphqlDataService.Sample.Tests.Exports;

public sealed class EntityExportJobServiceTests
{
    [Fact]
    public async Task EnqueueAsync_ValidRequest_PersistsQueuedJob()
    {
        var store = new RecordingStore();
        var service = CreateService(store, DefaultGrid());

        var result = await service.EnqueueAsync(
            ExportFileFormat.Csv,
            ["id", "name"],
            [new ExportFilterInput
            {
                Field = "name",
                Operator = ExportFilterOperator.Contains,
                Value = "Ada"
            }],
            [new ExportOrderInput
            {
                Field = "name",
                Direction = ExportSortDirection.Ascending
            }],
            TestContext.Current.CancellationToken);

        Assert.NotNull(store.Created);
        Assert.Equal(ExportStatus.Queued, store.Created.Status);
        Assert.Equal("Customer", store.Created.EntityName);
        Assert.Equal(result.ExportId, store.Created.ExportId);
        Assert.Equal($"/exports/{result.ExportId:D}/download", result.DownloadUrl);
    }

    [Fact]
    public async Task EnqueueByGridViewAsync_UsesVisibleColumnsAndStoredQuery()
    {
        var store = new RecordingStore();
        var grid = DefaultGrid() with
        {
            GridViewName = "named",
            ExportFormat = ExportFileFormat.Excel,
            Filters = [new ExportFilterInput
            {
                Field = "name",
                Operator = ExportFilterOperator.Equal,
                Value = "Ada"
            }],
            Order = [new ExportOrderInput
            {
                Field = "id",
                Direction = ExportSortDirection.Descending
            }]
        };
        var service = CreateService(store, grid);

        await service.EnqueueByGridViewAsync("named", TestContext.Current.CancellationToken);

        Assert.NotNull(store.Created);
        Assert.Equal(ExportRequestType.GridView, store.Created.RequestType);
        Assert.Equal(ExportFileFormat.Excel, store.Created.Format);
        Assert.Equal(["id", "name"], store.Created.Columns);
        Assert.Equal("named", store.Created.GridViewName);
    }

    [Theory]
    [InlineData("empty", "EXPORT_COLUMNS_REQUIRED")]
    [InlineData("duplicate", "EXPORT_COLUMN_DUPLICATE")]
    [InlineData("unknown", "EXPORT_COLUMN_INVALID")]
    public async Task EnqueueAsync_InvalidColumns_ReturnsStableCode(
        string caseName,
        string expectedCode)
    {
        var columns = caseName switch
        {
            "empty" => Array.Empty<string>(),
            "duplicate" => ["id", "id"],
            _ => ["unknown"]
        };
        var service = CreateService(new RecordingStore(), DefaultGrid());

        var exception = await Assert.ThrowsAsync<GraphQLException>(() => service.EnqueueAsync(
            ExportFileFormat.Csv,
            columns,
            null,
            null,
            TestContext.Current.CancellationToken));

        Assert.Equal(expectedCode, Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public async Task EnqueueAsync_GridDisallowsFilter_ReturnsInvalidFilter()
    {
        var columns = DefaultGrid().Columns
            .Select(column => column.SourceGraphqlColumn == "name"
                ? column with { EnableFiltering = false }
                : column)
            .ToArray();
        var service = CreateService(
            new RecordingStore(),
            DefaultGrid() with { Columns = columns });

        var exception = await Assert.ThrowsAsync<GraphQLException>(() => service.EnqueueAsync(
            ExportFileFormat.Csv,
            ["name"],
            [new ExportFilterInput
            {
                Field = "name",
                Operator = ExportFilterOperator.Equal,
                Value = "Ada"
            }],
            null,
            TestContext.Current.CancellationToken));

        Assert.Equal("EXPORT_FILTER_INVALID", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public async Task EnqueueAsync_GridDisallowsOrDuplicatesOrder_ReturnsInvalidOrder()
    {
        var service = CreateService(new RecordingStore(), DefaultGrid());
        var order = new ExportOrderInput
        {
            Field = "name",
            Direction = ExportSortDirection.Ascending
        };

        var exception = await Assert.ThrowsAsync<GraphQLException>(() => service.EnqueueAsync(
            ExportFileFormat.Csv,
            ["name"],
            null,
            [order, order],
            TestContext.Current.CancellationToken));

        Assert.Equal("EXPORT_ORDER_INVALID", Assert.Single(exception.Errors).Code);
    }

    private static EntityExportJobService<CustomerEntity> CreateService(
        RecordingStore store,
        GridDefinition grid) =>
        new(
            store,
            new GridRegistry(grid),
            new CustomerQueryCapabilities(),
            Options.Create(new ExportConfig
            {
                SharedStorageRoot = IOPath.GetTempPath(),
                DefaultFormat = ExportFileFormat.Csv,
                ExpiryMinutes = 10
            }));

    private static GridDefinition DefaultGrid() => new(
        "Customer",
        "default",
        null,
        [
            new("Id", GridColumnType.Uuid, null, TextAlignment.Left, true, 100, true, true, "id"),
            new("Name", GridColumnType.String, null, TextAlignment.Left, true, 200, true, true, "name"),
            new("Email", GridColumnType.String, null, TextAlignment.Left, false, 200, true, true, "email")
        ]);

    private sealed class GridRegistry(GridDefinition grid) : IEntityGridDefinitionRegistry
    {
        public Task<GridDefinition> GetRequiredAsync(
            string entityName,
            string gridViewName,
            CancellationToken cancellationToken) => Task.FromResult(grid);
    }

    private sealed class RecordingStore : IExportJobStore
    {
        public ExportJobRecord? Created { get; private set; }
        public string RootPath => IOPath.GetTempPath();
        public Task CreateAsync(ExportJobRecord job, CancellationToken cancellationToken)
        {
            Created = job;
            return Task.CompletedTask;
        }

        public Task<ExportJobRecord?> ReadAsync(Guid exportId, CancellationToken cancellationToken) =>
            Task.FromResult<ExportJobRecord?>(null);
        public Task WriteAsync(ExportJobRecord job, CancellationToken cancellationToken) => Task.CompletedTask;
        public async IAsyncEnumerable<Guid> ListAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
        {
            await Task.CompletedTask;
            yield break;
        }
        public Task<ExportJobRecord?> TryClaimAsync(Guid exportId, string serverName, CancellationToken cancellationToken) =>
            Task.FromResult<ExportJobRecord?>(null);
        public void ReleaseClaim(Guid exportId) { }
        public string GetJobFolder(Guid exportId) => IOPath.Combine(RootPath, exportId.ToString("D"));
        public string GetFilePath(Guid exportId, string fileName) => IOPath.Combine(GetJobFolder(exportId), fileName);
        public Task DeleteAsync(Guid exportId, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
