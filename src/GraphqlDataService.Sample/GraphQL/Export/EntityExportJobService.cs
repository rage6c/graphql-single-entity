using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Grid;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed class EntityExportJobService<TEntity>(
    IExportJobStore store,
    IEntityGridDefinitionRegistry grids,
    IEntityExportQueryCapabilities<TEntity> queryCapabilities,
    IOptions<ExportConfig> options) : IEntityExportJobService<TEntity>
{
    public async Task<ExportJob> EnqueueAsync(
        ExportFileFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<ExportFilterInput>? filters,
        IReadOnlyList<ExportOrderInput>? order,
        CancellationToken cancellationToken)
    {
        var grid = await grids.GetRequiredAsync(typeof(TEntity).Name, "default", cancellationToken);
        ValidateColumns(columns, grid);
        filters ??= [];
        order ??= [];
        queryCapabilities.Validate(filters, order);
        ValidateQuery(filters, order, grid);
        return await CreateAsync(
            format,
            columns,
            filters,
            order,
            null,
            ExportRequestType.ColumnSelector,
            cancellationToken);
    }

    public async Task<ExportJob> EnqueueByGridViewAsync(
        string gridViewName,
        CancellationToken cancellationToken)
    {
        var grid = await grids.GetRequiredAsync(typeof(TEntity).Name, gridViewName, cancellationToken);
        var columns = grid.Columns.Where(x => x.Visibility).Select(x => x.SourceGraphqlColumn).ToArray();
        ValidateColumns(columns, grid);
        var filters = grid.Filters ?? [];
        var order = grid.Order ?? [];
        queryCapabilities.Validate(filters, order);
        ValidateQuery(filters, order, grid);
        return await CreateAsync(
            grid.ExportFormat ?? options.Value.DefaultFormat,
            columns,
            filters,
            order,
            gridViewName,
            ExportRequestType.GridView,
            cancellationToken);
    }

    private async Task<ExportJob> CreateAsync(
        ExportFileFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<ExportFilterInput> filters,
        IReadOnlyList<ExportOrderInput> order,
        string? gridViewName,
        ExportRequestType requestType,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var exportId = Guid.NewGuid();
        var record = new ExportJobRecord
        {
            ExportId = exportId,
            EntityName = typeof(TEntity).Name,
            RequestType = requestType,
            Format = format,
            Columns = [.. columns],
            Filters = [.. filters],
            Order = [.. order],
            GridViewName = gridViewName,
            RequestedBy = "anonymous",
            TenantId = null,
            RequestedUtc = now,
            Status = ExportStatus.Queued,
            ExpiresUtc = now.AddMinutes(options.Value.ExpiryMinutes)
        };

        await store.CreateAsync(record, cancellationToken);
        return new ExportJob(exportId, record.Status, $"/exports/{exportId:D}/download", record.ExpiresUtc);
    }

    private static void ValidateColumns(IReadOnlyList<string> columns, GridDefinition grid)
    {
        if (columns.Count == 0)
        {
            throw Error("At least one export column is required.", "EXPORT_COLUMNS_REQUIRED");
        }

        if (columns.Distinct(StringComparer.Ordinal).Count() != columns.Count)
        {
            throw Error("Export columns must be unique.", "EXPORT_COLUMN_DUPLICATE");
        }

        var allowed = grid.Columns.Select(x => x.SourceGraphqlColumn).ToHashSet(StringComparer.Ordinal);
        if (columns.Any(column => !allowed.Contains(column)))
        {
            throw Error("An export column is invalid.", "EXPORT_COLUMN_INVALID");
        }
    }

    private static GraphQLException Error(string message, string code) =>
        new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());

    private static void ValidateQuery(
        IReadOnlyList<ExportFilterInput> filters,
        IReadOnlyList<ExportOrderInput> order,
        GridDefinition grid)
    {
        var filterable = grid.Columns
            .Where(column => column.EnableFiltering)
            .Select(column => column.SourceGraphqlColumn)
            .ToHashSet(StringComparer.Ordinal);
        if (filters.Any(filter =>
                string.IsNullOrWhiteSpace(filter.Value) || !filterable.Contains(filter.Field)))
        {
            throw Error("An export filter is invalid.", "EXPORT_FILTER_INVALID");
        }

        var sortable = grid.Columns
            .Where(column => column.EnableSorting)
            .Select(column => column.SourceGraphqlColumn)
            .ToHashSet(StringComparer.Ordinal);
        if (order.Any(item => !sortable.Contains(item.Field)) ||
            order.Select(item => item.Field).Distinct(StringComparer.Ordinal).Count() != order.Count)
        {
            throw Error("An export order is invalid.", "EXPORT_ORDER_INVALID");
        }
    }
}
