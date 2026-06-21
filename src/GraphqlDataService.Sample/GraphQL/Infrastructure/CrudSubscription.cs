using System.Runtime.CompilerServices;
using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL.Infrastructure;

public abstract class CrudSubscription<TEntity>
    where TEntity : class
{
    protected async IAsyncEnumerable<ExportJobStatusUpdate> SubscribeToExportStatusAsync(
        Guid exportId,
        IExportJobStore store,
        IOptions<ExportConfig> options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ExportStatus? previousStatus = null;
        while (!cancellationToken.IsCancellationRequested)
        {
            var job = await store.ReadAsync(exportId, cancellationToken);
            if (job is null || job.EntityName != typeof(TEntity).Name)
            {
                if (previousStatus is null)
                {
                    throw new GraphQLException(ErrorBuilder.New()
                        .SetMessage("Export job was not found.")
                        .SetCode("EXPORT_JOB_NOT_FOUND")
                        .Build());
                }

                yield break;
            }

            var status = ResolveStatus(job);
            if (status != previousStatus)
            {
                yield return CreateUpdate(job, status);
                previousStatus = status;
            }

            if (IsTerminal(status))
            {
                yield break;
            }

            await Task.Delay(
                TimeSpan.FromSeconds(options.Value.StatusPollingIntervalSeconds),
                cancellationToken);
        }
    }

    private static ExportStatus ResolveStatus(ExportJobRecord job) =>
        job.ExpiresUtc <= DateTimeOffset.UtcNow &&
        job.Status is not (ExportStatus.Completed or ExportStatus.Downloaded)
            ? ExportStatus.Expired
            : job.Status;

    private static ExportJobStatusUpdate CreateUpdate(
        ExportJobRecord job,
        ExportStatus status) =>
        new(
            job.ExportId,
            status,
            status == ExportStatus.Completed
                ? $"/exports/{job.ExportId:D}/download"
                : null,
            job.ExpiresUtc,
            status == ExportStatus.Failed ? job.ErrorCode ?? "EXPORT_FAILED" : null);

    private static bool IsTerminal(ExportStatus status) =>
        status is ExportStatus.Completed or ExportStatus.Failed or
            ExportStatus.Downloaded or ExportStatus.Expired;
}
