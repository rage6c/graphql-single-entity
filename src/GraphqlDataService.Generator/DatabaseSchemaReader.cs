using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace GraphqlDataService.Generator;

public sealed class DatabaseSchemaReader(DatabaseProvider provider)
{
    public async Task<IReadOnlyList<DatabaseTable>> ReadAsync(
        SchemaDbContext db,
        string schema,
        IReadOnlyList<string> selectedTables,
        CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = provider == DatabaseProvider.PostgreSql
            ? PostgreSqlQuery
            : SqlServerQuery;
        AddParameter(command, "@schema", schema);

        var columns = new List<DatabaseColumn>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            columns.Add(new DatabaseColumn(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4),
                reader.GetBoolean(5),
                reader.GetBoolean(6),
                reader.IsDBNull(7) ? null : reader.GetInt32(7),
                reader.GetInt32(8)));
        }

        var selected = selectedTables.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return columns
            .Where(column => selected.Count == 0 || selected.Contains(column.Table))
            .GroupBy(column => (column.Schema, column.Table))
            .Select(group => new DatabaseTable(
                group.Key.Schema,
                group.Key.Table,
                group.OrderBy(column => column.Ordinal).ToArray()))
            .OrderBy(table => table.Name, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private const string PostgreSqlQuery = """
        SELECT c.table_schema,
               c.table_name,
               c.column_name,
               c.udt_name,
               c.is_nullable = 'YES' AS is_nullable,
               EXISTS (
                   SELECT 1
                   FROM information_schema.table_constraints tc
                   JOIN information_schema.key_column_usage kcu
                     ON tc.constraint_name = kcu.constraint_name
                    AND tc.constraint_schema = kcu.constraint_schema
                  WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_schema = c.table_schema
                    AND tc.table_name = c.table_name
                    AND kcu.column_name = c.column_name) AS is_primary_key,
               c.is_identity = 'YES' OR COALESCE(c.column_default LIKE 'nextval(%', false) AS is_identity,
               c.character_maximum_length::integer,
               c.ordinal_position
          FROM information_schema.columns c
         WHERE c.table_schema = @schema
         ORDER BY c.table_name, c.ordinal_position;
        """;

    private const string SqlServerQuery = """
        SELECT c.TABLE_SCHEMA,
               c.TABLE_NAME,
               c.COLUMN_NAME,
               c.DATA_TYPE,
               CAST(CASE WHEN c.IS_NULLABLE = 'YES' THEN 1 ELSE 0 END AS bit),
               CAST(CASE WHEN EXISTS (
                   SELECT 1
                     FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                     JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                       ON tc.CONSTRAINT_NAME = kcu.CONSTRAINT_NAME
                      AND tc.CONSTRAINT_SCHEMA = kcu.CONSTRAINT_SCHEMA
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                      AND tc.TABLE_SCHEMA = c.TABLE_SCHEMA
                      AND tc.TABLE_NAME = c.TABLE_NAME
                      AND kcu.COLUMN_NAME = c.COLUMN_NAME) THEN 1 ELSE 0 END AS bit),
               CAST(CASE WHEN COLUMNPROPERTY(
                   OBJECT_ID(QUOTENAME(c.TABLE_SCHEMA) + '.' + QUOTENAME(c.TABLE_NAME)),
                   c.COLUMN_NAME,
                   'IsIdentity') = 1 THEN 1 ELSE 0 END AS bit),
               CASE WHEN c.CHARACTER_MAXIMUM_LENGTH < 0 THEN NULL ELSE c.CHARACTER_MAXIMUM_LENGTH END,
               c.ORDINAL_POSITION
          FROM INFORMATION_SCHEMA.COLUMNS c
         WHERE c.TABLE_SCHEMA = @schema
         ORDER BY c.TABLE_NAME, c.ORDINAL_POSITION;
        """;
}
