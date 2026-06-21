using System.Text;

namespace GraphqlDataService.Generator;

public sealed class EntityModelFactory(DatabaseProvider provider, string rootNamespace)
{
    public EntityModel Create(DatabaseTable table)
    {
        var entityName = Singularize(ToPascalCase(table.Name));
        var columns = table.Columns.Select(CreateColumn).ToArray();
        var keys = columns.Where(column => column.IsPrimaryKey).ToArray();
        if (keys.Length != 1)
        {
            throw new InvalidOperationException(
                $"Table '{table.Schema}.{table.Name}' must have exactly one primary key column.");
        }

        var key = keys[0];
        if (!key.IsIdentity && key.NonNullableClrType != "Guid")
        {
            throw new InvalidOperationException(
                $"Key '{table.Schema}.{table.Name}.{key.ColumnName}' must be identity-generated or Guid.");
        }

        var inputs = columns.Where(column => !column.IsPrimaryKey && !column.IsAudit).ToArray();
        return new EntityModel(
            rootNamespace,
            $"{rootNamespace}.Provider.{entityName}",
            entityName,
            LowerFirst(entityName),
            Pluralize(entityName),
            LowerFirst(Pluralize(entityName)),
            table.Name,
            table.Schema,
            key.NonNullableClrType,
            key.PropertyName,
            key.GraphQlName,
            !key.IsIdentity && key.NonNullableClrType == "Guid",
            columns,
            columns.Where(column => !column.IsAudit).ToArray(),
            inputs,
            inputs.Where(column => column.IsString && !column.IsNullable).ToArray(),
            columns.Any(column => column.PropertyName == "CreatedAt"),
            columns.Any(column => column.PropertyName == "UpdatedAt"));
    }

    private ColumnModel CreateColumn(DatabaseColumn column)
    {
        var nonNullableType = MapType(column.StoreType);
        var nullable = column.IsNullable && nonNullableType != "byte[]"
            ? "?"
            : string.Empty;
        var propertyName = ToPascalCase(column.Name);
        var isAudit = propertyName is "CreatedAt" or "UpdatedAt" or "CreatedBy" or "UpdatedBy";
        var clrType = nonNullableType + nullable;
        return new ColumnModel(
            column.Name,
            propertyName,
            LowerFirst(propertyName),
            clrType,
            nonNullableType,
            column.IsNullable,
            column.IsPrimaryKey,
            column.IsIdentity,
            nonNullableType == "string",
            isAudit,
            column.MaxLength,
            isAudit ? string.Empty : BuildFieldDefinition(
                ToPascalCase(column.Table),
                LowerFirst(Singularize(ToPascalCase(column.Table))),
                propertyName,
                LowerFirst(propertyName),
                clrType,
                nonNullableType,
                column.IsNullable));
    }

    private string MapType(string storeType)
    {
        var normalized = storeType.ToLowerInvariant();
        return provider switch
        {
            DatabaseProvider.PostgreSql => normalized switch
            {
                "uuid" => "Guid",
                "int2" => "short",
                "int4" => "int",
                "int8" => "long",
                "bool" => "bool",
                "numeric" or "money" => "decimal",
                "float4" => "float",
                "float8" => "double",
                "date" => "DateOnly",
                "time" or "timetz" => "TimeOnly",
                "timestamp" => "DateTime",
                "timestamptz" => "DateTimeOffset",
                "text" or "varchar" or "bpchar" or "citext" or "json" or "jsonb" => "string",
                _ => throw Unsupported(storeType)
            },
            DatabaseProvider.SqlServer => normalized switch
            {
                "uniqueidentifier" => "Guid",
                "smallint" => "short",
                "int" => "int",
                "bigint" => "long",
                "tinyint" => "byte",
                "bit" => "bool",
                "decimal" or "numeric" or "money" or "smallmoney" => "decimal",
                "real" => "float",
                "float" => "double",
                "date" => "DateOnly",
                "time" => "TimeOnly",
                "datetime" or "datetime2" or "smalldatetime" => "DateTime",
                "datetimeoffset" => "DateTimeOffset",
                "char" or "nchar" or "varchar" or "nvarchar" or "text" or "ntext" or "xml" => "string",
                _ => throw Unsupported(storeType)
            },
            _ => throw new ArgumentOutOfRangeException()
        };
    }

    private static string BuildFieldDefinition(
        string tableEntityName,
        string variable,
        string property,
        string graphQlName,
        string clrType,
        string nonNullableType,
        bool nullable)
    {
        var entityName = Singularize(tableEntityName);
        if (nonNullableType == "string")
        {
            return $$"""
                new EntityExportField<{{entityName}}Entity, {{clrType}}>(
                    "{{graphQlName}}",
                    {{variable}} => {{variable}}.{{property}},
                    Set(ExportFilterOperator.Equal, ExportFilterOperator.Contains),
                    (query, filter) => filter.Operator switch
                    {
                        ExportFilterOperator.Equal => query.Where({{variable}} => {{variable}}.{{property}} == filter.Value),
                        ExportFilterOperator.Contains => query.Where({{variable}} => {{variable}}.{{property}} != null && {{variable}}.{{property}}.Contains(filter.Value)),
                        _ => throw InvalidFilter("{{graphQlName}}")
                    },
                    {{variable}} => {{variable}}.{{property}} ?? string.Empty)
                """;
        }

        var parse = ParseExpression(nonNullableType);
        var value = nullable ? "value" : "value";
        var format = FormatExpression(variable, property, nonNullableType, nullable);
        if (nonNullableType is "Guid" or "bool")
        {
            return $$"""
                new EntityExportField<{{entityName}}Entity, {{clrType}}>(
                    "{{graphQlName}}",
                    {{variable}} => {{variable}}.{{property}},
                    Set(ExportFilterOperator.Equal),
                    (query, filter) => {{parse}}
                        ? query.Where({{variable}} => {{variable}}.{{property}} == value)
                        : throw InvalidFilter("{{graphQlName}}"),
                    {{variable}} => {{format}})
                """;
        }

        return $$"""
            new EntityExportField<{{entityName}}Entity, {{clrType}}>(
                "{{graphQlName}}",
                {{variable}} => {{variable}}.{{property}},
                Set(ExportFilterOperator.Equal, ExportFilterOperator.GreaterThanOrEqual, ExportFilterOperator.LessThanOrEqual),
                (query, filter) => {{parse}}
                    ? filter.Operator switch
                    {
                        ExportFilterOperator.Equal => query.Where({{variable}} => {{variable}}.{{property}} == {{value}}),
                        ExportFilterOperator.GreaterThanOrEqual => query.Where({{variable}} => {{variable}}.{{property}} >= {{value}}),
                        ExportFilterOperator.LessThanOrEqual => query.Where({{variable}} => {{variable}}.{{property}} <= {{value}}),
                        _ => throw InvalidFilter("{{graphQlName}}")
                    }
                    : throw InvalidFilter("{{graphQlName}}"),
                {{variable}} => {{format}})
            """;
    }

    private static string ParseExpression(string type) => type switch
    {
        "Guid" => "Guid.TryParse(filter.Value, out var value)",
        "DateOnly" => "DateOnly.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)",
        "TimeOnly" => "TimeOnly.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)",
        "DateTime" => "DateTime.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)",
        "DateTimeOffset" => "DateTimeOffset.TryParse(filter.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var value)",
        "bool" => "bool.TryParse(filter.Value, out var value)",
        "short" => "short.TryParse(filter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)",
        "int" => "int.TryParse(filter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)",
        "long" => "long.TryParse(filter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)",
        "byte" => "byte.TryParse(filter.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)",
        "decimal" => "decimal.TryParse(filter.Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var value)",
        "float" => "float.TryParse(filter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)",
        "double" => "double.TryParse(filter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)",
        _ => throw new InvalidOperationException($"Export parsing is not supported for '{type}'.")
    };

    private static string FormatExpression(string variable, string property, string type, bool nullable)
    {
        var access = $"{variable}.{property}";
        return type switch
        {
            "Guid" => nullable ? $"{access}?.ToString(\"D\") ?? string.Empty" : $"{access}.ToString(\"D\")",
            "DateOnly" => nullable
                ? $"{access}?.ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture) ?? string.Empty"
                : $"{access}.ToString(\"yyyy-MM-dd\", CultureInfo.InvariantCulture)",
            "TimeOnly" => nullable
                ? $"{access}?.ToString(\"HH:mm:ss.fffffff\", CultureInfo.InvariantCulture) ?? string.Empty"
                : $"{access}.ToString(\"HH:mm:ss.fffffff\", CultureInfo.InvariantCulture)",
            "DateTime" or "DateTimeOffset" => nullable
                ? $"{access}?.ToString(\"O\", CultureInfo.InvariantCulture) ?? string.Empty"
                : $"{access}.ToString(\"O\", CultureInfo.InvariantCulture)",
            _ => $"Convert.ToString({access}, CultureInfo.InvariantCulture) ?? string.Empty"
        };
    }

    private static Exception Unsupported(string storeType) =>
        new InvalidOperationException($"Database type '{storeType}' is not supported.");

    public static string ToPascalCase(string value)
    {
        var result = new StringBuilder();
        var upper = true;
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                upper = true;
                continue;
            }

            result.Append(upper ? char.ToUpperInvariant(character) : character);
            upper = false;
        }

        return result.ToString();
    }

    private static string LowerFirst(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToLowerInvariant(value[0]) + value[1..];

    private static string Singularize(string value)
    {
        if (value.EndsWith("ies", StringComparison.OrdinalIgnoreCase)) return value[..^3] + "y";
        if (value.EndsWith("sses", StringComparison.OrdinalIgnoreCase)) return value[..^2];
        if (value.EndsWith("ches", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("shes", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("xes", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("zes", StringComparison.OrdinalIgnoreCase)) return value[..^2];
        return value.EndsWith('s') && !value.EndsWith("ss", StringComparison.OrdinalIgnoreCase)
            ? value[..^1]
            : value;
    }

    private static string Pluralize(string value)
    {
        if (value.EndsWith('y') && value.Length > 1 && !"aeiou".Contains(char.ToLowerInvariant(value[^2])))
            return value[..^1] + "ies";
        if (value.EndsWith('s') || value.EndsWith('x') || value.EndsWith('z') ||
            value.EndsWith("ch", StringComparison.OrdinalIgnoreCase) ||
            value.EndsWith("sh", StringComparison.OrdinalIgnoreCase)) return value + "es";
        return value + "s";
    }
}
