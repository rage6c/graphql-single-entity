using GraphqlDataService.Sample.Configuration;
using Microsoft.Extensions.Options;
using IOPath = System.IO.Path;

namespace GraphqlDataService.Sample.GraphQL.Export;

public static class ExportEndpointExtensions
{
    public static IEndpointRouteBuilder MapExportDownloads(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/exports/{exportId:guid}/download", DownloadAsync);
        return endpoints;
    }

    internal static async Task<IResult> DownloadAsync(
        Guid exportId,
        HttpContext httpContext,
        IExportJobStore store,
        IOptions<ExportConfig> options,
        CancellationToken cancellationToken)
    {
        var job = await store.ReadAsync(exportId, cancellationToken);
        if (job is null || job.ExpiresUtc <= DateTimeOffset.UtcNow)
        {
            return Results.NotFound();
        }

        if (job.Status is ExportStatus.Queued or ExportStatus.Claimed or ExportStatus.Running)
        {
            httpContext.Response.Headers.RetryAfter = "5";
            return Results.Accepted();
        }

        if (job.Status == ExportStatus.Failed)
        {
            return Results.UnprocessableEntity(new { code = job.ErrorCode ?? "EXPORT_FAILED" });
        }

        if (job.Status != ExportStatus.Completed || job.FileName is null || job.ContentType is null)
        {
            return Results.NotFound();
        }

        var path = store.GetFilePath(exportId, job.FileName);
        if (!File.Exists(path) || new FileInfo(path).Length != job.FileBytes)
        {
            return Results.NotFound();
        }

        httpContext.Response.Headers.CacheControl = "no-store";
        if (options.Value.DeleteJobFolderAfterDownload)
        {
            httpContext.Response.OnCompleted(() => store.DeleteAsync(exportId, CancellationToken.None));
        }

        return Results.File(path, job.ContentType, IOPath.GetFileName(job.FileName), enableRangeProcessing: false);
    }
}
