namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed record GeneratedExport(string FileName, string ContentType, int RowCount, long FileBytes);

public interface IExportGenerator
{
    string EntityName { get; }
    Task<GeneratedExport> GenerateAsync(ExportJobRecord job, CancellationToken cancellationToken);
}
