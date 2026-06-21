using GraphqlDataService.Sample.Configuration;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed class ExportCleanupService(
    IExportJobStore store,
    IOptions<ExportConfig> options,
    ILogger<ExportCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await foreach (var exportId in store.ListAsync(stoppingToken))
            {
                try
                {
                    var job = await store.ReadAsync(exportId, stoppingToken);
                    if (job is not null && job.ExpiresUtc <= DateTimeOffset.UtcNow &&
                        job.Status is not (ExportStatus.Claimed or ExportStatus.Running or ExportStatus.Downloading))
                    {
                        await store.DeleteAsync(exportId, stoppingToken);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception, "Failed to clean export {ExportId}", exportId);
                }
            }
        }
    }
}
