using System.Linq.Expressions;
using System.Text;
using ClosedXML.Excel;
using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL.Export;

public abstract class CrudExportGenerator<TEntity>(
    IDbContextFactory<AppDbContext> dbContextFactory,
    IExportJobStore store,
    IEntityExportQueryCapabilities<TEntity> queryCapabilities,
    IOptions<ExportConfig> options) : IExportGenerator
    where TEntity : EntityBase
{
    private static readonly IReadOnlyDictionary<string, System.Reflection.PropertyInfo>
        EntityProperties = typeof(TEntity)
            .GetProperties()
            .Where(property =>
                property is { CanRead: true, CanWrite: true } &&
                property.GetIndexParameters().Length == 0)
            .ToDictionary(property => property.Name, StringComparer.OrdinalIgnoreCase);

    public string EntityName => typeof(TEntity).Name;

    protected abstract string FileNamePrefix { get; }
    protected abstract string WorksheetName { get; }

    public async Task<GeneratedExport> GenerateAsync(
        ExportJobRecord job,
        CancellationToken cancellationToken)
    {
        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Set<TEntity>().AsNoTracking();
        query = queryCapabilities.ApplyFilters(query, job.Filters);
        query = queryCapabilities.ApplyOrdering(query, job.Order);

        var rows = await query
            .Take(options.Value.MaxRows + 1)
            .Select(BuildProjection(job.Columns))
            .ToListAsync(cancellationToken);
        if (rows.Count > options.Value.MaxRows)
        {
            throw new ExportGenerationException(
                "EXPORT_LIMIT_EXCEEDED",
                "Export row limit exceeded.");
        }

        var extension = job.Format == ExportFileFormat.Csv ? "csv" : "xlsx";
        var fileName = $"{FileNamePrefix}.{extension}";
        var path = store.GetFilePath(job.ExportId, fileName);
        try
        {
            if (job.Format == ExportFileFormat.Csv)
            {
                await WriteCsvAsync(path, rows, job.Columns, cancellationToken);
            }
            else
            {
                WriteExcel(path, rows, job.Columns, cancellationToken);
            }

            var size = new FileInfo(path).Length;
            if (size > options.Value.MaxBytes)
            {
                throw new ExportGenerationException(
                    "EXPORT_LIMIT_EXCEEDED",
                    "Export byte limit exceeded.");
            }

            var contentType = job.Format == ExportFileFormat.Csv
                ? "text/csv; charset=utf-8"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            return new GeneratedExport(fileName, contentType, rows.Count, size);
        }
        catch
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            throw;
        }
    }

    private Expression<Func<TEntity, TEntity>> BuildProjection(
        IReadOnlyList<string> columns)
    {
        var entity = Expression.Parameter(typeof(TEntity), "entity");
        var bindings = columns.Select(column =>
        {
            var propertyName = queryCapabilities.GetPropertyName(column);
            if (!EntityProperties.TryGetValue(propertyName, out var property))
            {
                throw new ExportGenerationException(
                    "EXPORT_COLUMN_INVALID",
                    $"Unsupported projection field '{column}'.");
            }

            return Expression.Bind(property, Expression.Property(entity, property));
        });

        return Expression.Lambda<Func<TEntity, TEntity>>(
            Expression.MemberInit(Expression.New(typeof(TEntity)), bindings),
            entity);
    }

    private async Task WriteCsvAsync(
        string path,
        IReadOnlyList<TEntity> rows,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            81920,
            useAsync: true);
        await using var writer = new StreamWriter(
            stream,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        await writer.WriteLineAsync(string.Join(',', columns.Select(Csv)));
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await writer.WriteLineAsync(string.Join(
                ',',
                columns.Select(column => Csv(queryCapabilities.GetValue(row, column)))));
        }
    }

    private void WriteExcel(
        string path,
        IReadOnlyList<TEntity> rows,
        IReadOnlyList<string> columns,
        CancellationToken cancellationToken)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add(WorksheetName);
        for (var column = 0; column < columns.Count; column++)
        {
            sheet.Cell(1, column + 1).Value = columns[column];
        }

        for (var row = 0; row < rows.Count; row++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (var column = 0; column < columns.Count; column++)
            {
                sheet.Cell(row + 2, column + 1).Value =
                    ExcelSafe(queryCapabilities.GetValue(rows[row], columns[column]));
            }
        }

        workbook.SaveAs(path);
    }

    private static string Csv(string value)
    {
        value = ExcelSafe(value);
        return value.IndexOfAny([',', '"', '\r', '\n']) >= 0
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }

    private static string ExcelSafe(string value) =>
        value.Length > 0 && value[0] is '=' or '+' or '-' or '@'
            ? $"'{value}"
            : value;
}
