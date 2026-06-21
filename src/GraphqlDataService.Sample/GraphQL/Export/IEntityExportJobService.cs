namespace GraphqlDataService.Sample.GraphQL.Export;

public interface IEntityExportJobService<TEntity>
{
    Task<ExportJob> EnqueueAsync(
        ExportFileFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<ExportFilterInput>? filters,
        IReadOnlyList<ExportOrderInput>? order,
        CancellationToken cancellationToken);

    Task<ExportJob> EnqueueByGridViewAsync(
        string gridViewName,
        CancellationToken cancellationToken);
}
