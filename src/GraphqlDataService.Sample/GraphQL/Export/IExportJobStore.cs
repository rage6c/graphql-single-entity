namespace GraphqlDataService.Sample.GraphQL.Export;

public interface IExportJobStore
{
    string RootPath { get; }
    Task CreateAsync(ExportJobRecord job, CancellationToken cancellationToken);
    Task<ExportJobRecord?> ReadAsync(Guid exportId, CancellationToken cancellationToken);
    IAsyncEnumerable<Guid> ListAsync(CancellationToken cancellationToken);
    Task<ExportJobRecord?> TryClaimAsync(Guid exportId, string serverName, CancellationToken cancellationToken);
    Task WriteAsync(ExportJobRecord job, CancellationToken cancellationToken);
    void ReleaseClaim(Guid exportId);
    string GetJobFolder(Guid exportId);
    string GetFilePath(Guid exportId, string fileName);
    Task DeleteAsync(Guid exportId, CancellationToken cancellationToken);
}
