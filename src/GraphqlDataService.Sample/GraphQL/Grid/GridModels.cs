using GraphqlDataService.Sample.GraphQL.Export;

namespace GraphqlDataService.Sample.GraphQL.Grid;

public enum GridColumnType
{
    String,
    Integer,
    Decimal,
    Boolean,
    Date,
    DateTime,
    Uuid,
    Enum
}

public enum TextAlignment
{
    Left,
    Center,
    Right
}

public sealed record GridColumnDefinition(
    string ColumnName,
    GridColumnType ColumnType,
    string? DisplayFormat,
    TextAlignment TextAlignment,
    bool Visibility,
    int? Width,
    bool EnableFiltering,
    bool EnableSorting,
    string SourceGraphqlColumn);

public sealed record GridDefinition(
    string EntityName,
    string GridViewName,
    ExportFileFormat? ExportFormat,
    IReadOnlyList<GridColumnDefinition> Columns,
    [property: GraphQLIgnore] IReadOnlyList<ExportFilterInput>? Filters = null,
    [property: GraphQLIgnore] IReadOnlyList<ExportOrderInput>? Order = null);

public interface IEntityGridDefinition
{
    string EntityName { get; }
    string GridViewName { get; }
    ExportFileFormat? ExportFormat { get; }
    IReadOnlyList<GridColumnDefinition> Columns { get; }
}
