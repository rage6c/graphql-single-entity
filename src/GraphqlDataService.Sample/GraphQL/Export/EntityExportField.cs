using System.Linq.Expressions;
using HotChocolate.Data.Filters;
using HotChocolate.Data.Sorting;

namespace GraphqlDataService.Sample.GraphQL.Export;

public interface IEntityExportField<TEntity>
{
    string Name { get; }
    string PropertyName { get; }
    IReadOnlySet<ExportFilterOperator> FilterOperators { get; }

    void ConfigureFiltering(IFilterInputTypeDescriptor<TEntity> descriptor);
    void ConfigureSorting(ISortInputTypeDescriptor<TEntity> descriptor);
    IQueryable<TEntity> ApplyFilter(IQueryable<TEntity> query, ExportFilterInput filter);
    IOrderedQueryable<TEntity> ApplyOrder(
        IQueryable<TEntity> query,
        IOrderedQueryable<TEntity>? ordered,
        ExportSortDirection direction);
    string Format(TEntity entity);
}

public sealed class EntityExportField<TEntity, TValue> : IEntityExportField<TEntity>
{
    private readonly Expression<Func<TEntity, TValue>> selector;
    private readonly Func<IQueryable<TEntity>, ExportFilterInput, IQueryable<TEntity>> applyFilter;
    private readonly Func<TEntity, string> formatter;

    public EntityExportField(
        string name,
        Expression<Func<TEntity, TValue>> selector,
        IReadOnlySet<ExportFilterOperator> filterOperators,
        Func<IQueryable<TEntity>, ExportFilterInput, IQueryable<TEntity>> applyFilter,
        Func<TEntity, string> formatter)
    {
        Name = name;
        this.selector = selector;
        FilterOperators = filterOperators;
        this.applyFilter = applyFilter;
        this.formatter = formatter;
        PropertyName = GetPropertyName(selector);
    }

    public string Name { get; }
    public string PropertyName { get; }
    public IReadOnlySet<ExportFilterOperator> FilterOperators { get; }

    public void ConfigureFiltering(IFilterInputTypeDescriptor<TEntity> descriptor) =>
        descriptor.Field(selector);

    public void ConfigureSorting(ISortInputTypeDescriptor<TEntity> descriptor) =>
        descriptor.Field(selector);

    public IQueryable<TEntity> ApplyFilter(
        IQueryable<TEntity> query,
        ExportFilterInput filter) => applyFilter(query, filter);

    public IOrderedQueryable<TEntity> ApplyOrder(
        IQueryable<TEntity> query,
        IOrderedQueryable<TEntity>? ordered,
        ExportSortDirection direction)
    {
        if (ordered is null)
        {
            return direction == ExportSortDirection.Ascending
                ? query.OrderBy(selector)
                : query.OrderByDescending(selector);
        }

        return direction == ExportSortDirection.Ascending
            ? ordered.ThenBy(selector)
            : ordered.ThenByDescending(selector);
    }

    public string Format(TEntity entity) => formatter(entity);

    private static string GetPropertyName(Expression<Func<TEntity, TValue>> expression)
    {
        var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? conversion.Operand
            : expression.Body;
        return body is MemberExpression member
            ? member.Member.Name
            : throw new ArgumentException("The selector must select an entity property.", nameof(expression));
    }
}
