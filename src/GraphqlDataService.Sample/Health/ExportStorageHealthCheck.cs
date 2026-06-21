using GraphqlDataService.Sample.GraphQL.Export;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace GraphqlDataService.Sample.Health;

public sealed class ExportStorageHealthCheck(IExportJobStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return Task.FromResult(Directory.Exists(store.RootPath)
                ? HealthCheckResult.Healthy("Shared export storage is available.")
                : HealthCheckResult.Unhealthy("Shared export storage is unavailable."));
        }
        catch (Exception exception)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(
                "Shared export storage check failed.", exception));
        }
    }
}
