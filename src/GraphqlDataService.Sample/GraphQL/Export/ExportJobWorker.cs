using GraphqlDataService.Sample.Configuration;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed class ExportJobWorker(
    IExportJobStore store,
    IServiceScopeFactory scopeFactory,
    IOptions<ExportConfig> options,
    ILogger<ExportJobWorker> logger) : BackgroundService
{
    private readonly string _serverName = Environment.MachineName;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.WorkerScanIntervalSeconds));
        do
        {
            await ProcessAvailableAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task ProcessAvailableAsync(CancellationToken cancellationToken)
    {
        await Parallel.ForEachAsync(
            store.ListAsync(cancellationToken),
            new ParallelOptions
            {
                MaxDegreeOfParallelism = options.Value.MaxConcurrentExports,
                CancellationToken = cancellationToken
            },
            async (exportId, token) => await ProcessJobAsync(exportId, token));
    }

    private async Task ProcessJobAsync(Guid exportId, CancellationToken cancellationToken)
    {
        var job = await store.TryClaimAsync(exportId, _serverName, cancellationToken);
        if (job is null)
        {
            return;
        }

        try
        {
            job.Status = ExportStatus.Running;
            job.StartedUtc ??= DateTimeOffset.UtcNow;
            await store.WriteAsync(job, cancellationToken);

            using var scope = scopeFactory.CreateScope();
            var generator = scope.ServiceProvider.GetServices<IExportGenerator>()
                .SingleOrDefault(item => item.EntityName == job.EntityName)
                ?? throw new ExportGenerationException("EXPORT_FAILED", "No exporter is registered.");
            var output = await generator.GenerateAsync(job, cancellationToken);

            job.Status = ExportStatus.Completed;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.LeaseUntilUtc = null;
            job.FileName = output.FileName;
            job.ContentType = output.ContentType;
            job.RowCount = output.RowCount;
            job.FileBytes = output.FileBytes;
            await store.WriteAsync(job, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            job.Status = ExportStatus.Failed;
            job.CompletedUtc = DateTimeOffset.UtcNow;
            job.LeaseUntilUtc = null;
            job.ErrorCode = exception is ExportGenerationException exportError
                ? exportError.Code
                : "EXPORT_FAILED";
            job.ErrorMessage = "Export generation failed.";
            await store.WriteAsync(job, cancellationToken);
            logger.LogError(exception, "Export {ExportId} failed on {ServerName}", exportId, _serverName);
        }
        finally
        {
            store.ReleaseClaim(exportId);
        }
    }
}
