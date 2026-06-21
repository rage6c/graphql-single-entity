namespace GraphqlDataService.Sample.GraphQL.Export;

public interface IEntityExportQueryCapabilities<TEntity>
{
    void Validate(
        IReadOnlyList<ExportFilterInput> filters,
        IReadOnlyList<ExportOrderInput> order);

    IQueryable<TEntity> ApplyFilters(
        IQueryable<TEntity> query,
        IReadOnlyList<ExportFilterInput> filters);

    IQueryable<TEntity> ApplyOrdering(
        IQueryable<TEntity> query,
        IReadOnlyList<ExportOrderInput> order);

    string GetValue(TEntity entity, string column);
    string GetPropertyName(string column);
}
