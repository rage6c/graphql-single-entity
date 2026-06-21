namespace GraphqlDataService.Generator;

public sealed record DatabaseColumn(
    string Schema,
    string Table,
    string Name,
    string StoreType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsIdentity,
    int? MaxLength,
    int Ordinal);

public sealed record DatabaseTable(
    string Schema,
    string Name,
    IReadOnlyList<DatabaseColumn> Columns);

public sealed record ColumnModel(
    string ColumnName,
    string PropertyName,
    string GraphQlName,
    string ClrType,
    string NonNullableClrType,
    bool IsNullable,
    bool IsPrimaryKey,
    bool IsIdentity,
    bool IsString,
    bool IsAudit,
    int? MaxLength,
    string FieldDefinition);

public sealed record EntityModel(
    string Namespace,
    string ProviderNamespace,
    string EntityName,
    string EntityVariable,
    string EntityPlural,
    string EntityPluralVariable,
    string TableName,
    string SchemaName,
    string KeyType,
    string KeyProperty,
    string KeyGraphQlName,
    bool GenerateGuidKey,
    IReadOnlyList<ColumnModel> Columns,
    IReadOnlyList<ColumnModel> ExportColumns,
    IReadOnlyList<ColumnModel> InputColumns,
    IReadOnlyList<ColumnModel> RequiredStringColumns,
    bool HasCreatedAt,
    bool HasUpdatedAt);
