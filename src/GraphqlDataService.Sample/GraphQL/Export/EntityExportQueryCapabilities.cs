using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;

namespace GraphqlDataService.Sample.GraphQL.Export;

public abstract class EntityExportQueryCapabilities<TEntity> :
    IEntityExportQueryCapabilities<TEntity>
{
    private IReadOnlyDictionary<string, IEntityExportField<TEntity>>? fieldsByName;

    protected abstract IReadOnlyList<IEntityExportField<TEntity>> Fields { get; }
    protected virtual string StableOrderField => "id";

    private IReadOnlyDictionary<string, IEntityExportField<TEntity>> FieldsByName =>
        fieldsByName ??= Fields.ToDictionary(definition => definition.Name, StringComparer.Ordinal);

    public void ConfigureFiltering(IFilterInputTypeDescriptor<TEntity> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        foreach (var field in Fields)
        {
            field.ConfigureFiltering(descriptor);
        }
    }

    public void ConfigureSorting(ISortInputTypeDescriptor<TEntity> descriptor)
    {
        descriptor.BindFieldsExplicitly();
        foreach (var field in Fields)
        {
            field.ConfigureSorting(descriptor);
        }
    }

    public void Validate(
        IReadOnlyList<ExportFilterInput> filters,
        IReadOnlyList<ExportOrderInput> order)
    {
        if (filters.Any(filter =>
                string.IsNullOrWhiteSpace(filter.Value) ||
                !FieldsByName.TryGetValue(filter.Field, out var field) ||
                !field.FilterOperators.Contains(filter.Operator)))
        {
            throw Error("An export filter is invalid.", "EXPORT_FILTER_INVALID");
        }

        if (order.Any(item => !FieldsByName.ContainsKey(item.Field)) ||
            order.Select(item => item.Field).Distinct(StringComparer.Ordinal).Count() != order.Count)
        {
            throw Error("An export order is invalid.", "EXPORT_ORDER_INVALID");
        }
    }

    public IQueryable<TEntity> ApplyFilters(
        IQueryable<TEntity> query,
        IReadOnlyList<ExportFilterInput> filters)
    {
        foreach (var filter in filters)
        {
            query = GetField(filter.Field, "EXPORT_FILTER_INVALID").ApplyFilter(query, filter);
        }

        return query;
    }

    public IQueryable<TEntity> ApplyOrdering(
        IQueryable<TEntity> query,
        IReadOnlyList<ExportOrderInput> order)
    {
        IOrderedQueryable<TEntity>? ordered = null;
        foreach (var item in order)
        {
            ordered = GetField(item.Field, "EXPORT_ORDER_INVALID")
                .ApplyOrder(query, ordered, item.Direction);
        }

        var stableField = GetField(StableOrderField, "EXPORT_ORDER_INVALID");
        return ordered is null
            ? stableField.ApplyOrder(query, null, ExportSortDirection.Ascending)
            : order.Any(item => item.Field == StableOrderField)
                ? ordered
                : stableField.ApplyOrder(query, ordered, ExportSortDirection.Ascending);
    }

    public string GetValue(TEntity entity, string column) =>
        GetField(column, "EXPORT_COLUMN_INVALID").Format(entity);

    public string GetPropertyName(string column) =>
        GetField(column, "EXPORT_COLUMN_INVALID").PropertyName;

    private IEntityExportField<TEntity> GetField(string name, string errorCode) =>
        FieldsByName.TryGetValue(name, out var field)
            ? field
            : throw new ExportGenerationException(errorCode, $"Unsupported field '{name}'.");

    private static GraphQLException Error(string message, string code) =>
        new(ErrorBuilder.New().SetMessage(message).SetCode(code).Build());
}
