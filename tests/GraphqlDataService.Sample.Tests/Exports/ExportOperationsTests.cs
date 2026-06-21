using System.Runtime.CompilerServices;
using System.Collections.Concurrent;
using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.Tests.Exports;

public sealed class ExportOperationsTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        $"graphql-export-operations-{Guid.NewGuid():N}");

    [Fact]
    public async Task Worker_ProcessesJobsWithBoundedConcurrency()
    {
        var jobs = Enumerable.Range(0, 4).Select(_ => Job(ExportStatus.Queued)).ToArray();
        var store = new FakeStore(root, jobs);
        var exporter = new FakeExporter(delayMilliseconds: 20);
        using var provider = Services(exporter);
        var worker = new ExportJobWorker(
            store,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(Config(maxConcurrent: 2)),
            NullLogger<ExportJobWorker>.Instance);

        await worker.ProcessAvailableAsync(TestContext.Current.CancellationToken);

        Assert.All(jobs, job => Assert.Equal(ExportStatus.Completed, job.Status));
        Assert.Equal(4, store.Released.Count);
        Assert.InRange(exporter.MaxActive, 1, 2);
    }

    [Fact]
    public async Task Worker_WhenExporterFails_RecordsSanitizedFailure()
    {
        var job = Job(ExportStatus.Queued);
        var store = new FakeStore(root, [job]);
        using var provider = Services(new FakeExporter(throws: true));
        var worker = new ExportJobWorker(
            store,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(Config()),
            NullLogger<ExportJobWorker>.Instance);

        await worker.ProcessAvailableAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Failed, job.Status);
        Assert.Equal("EXPORT_FAILED", job.ErrorCode);
        Assert.Equal("Export generation failed.", job.ErrorMessage);
        Assert.Contains(job.ExportId, store.Released);
    }

    [Fact]
    public async Task Worker_WithoutMatchingExporter_RecordsFailure()
    {
        var job = Job(ExportStatus.Queued);
        job.EntityName = "Unknown";
        var store = new FakeStore(root, [job]);
        using var provider = Services(new FakeExporter());
        var worker = new ExportJobWorker(
            store,
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(Config()),
            NullLogger<ExportJobWorker>.Instance);

        await worker.ProcessAvailableAsync(TestContext.Current.CancellationToken);

        Assert.Equal(ExportStatus.Failed, job.Status);
        Assert.Equal("EXPORT_FAILED", job.ErrorCode);
    }

    [Fact]
    public async Task Cleanup_DeletesOnlyExpiredInactiveJobsAndContinuesAfterError()
    {
        var expired = WithExpiry(Job(ExportStatus.Completed), DateTimeOffset.UtcNow.AddMinutes(-1));
        var active = WithExpiry(Job(ExportStatus.Running), DateTimeOffset.UtcNow.AddMinutes(-1));
        var future = WithExpiry(Job(ExportStatus.Completed), DateTimeOffset.UtcNow.AddMinutes(1));
        var failing = WithExpiry(Job(ExportStatus.Failed), DateTimeOffset.UtcNow.AddMinutes(-1));
        var store = new FakeStore(root, [expired, active, future, failing])
        {
            ThrowOnDelete = failing.ExportId
        };
        var cleanup = new ExportCleanupService(
            store,
            Options.Create(Config()),
            NullLogger<ExportCleanupService>.Instance);

        await cleanup.CleanupOnceAsync(TestContext.Current.CancellationToken);

        Assert.Contains(expired.ExportId, store.Deleted);
        Assert.DoesNotContain(active.ExportId, store.Deleted);
        Assert.DoesNotContain(future.ExportId, store.Deleted);
    }

    [Fact]
    public async Task DownloadEndpoint_ReturnsExpectedStatusForJobStates()
    {
        var queued = Job(ExportStatus.Queued);
        var failed = Job(ExportStatus.Failed);
        failed.ErrorCode = "EXPORT_FAILED";
        var expired = WithExpiry(Job(ExportStatus.Completed), DateTimeOffset.UtcNow.AddMinutes(-1));
        var store = new FakeStore(root, [queued, failed, expired]);
        var options = Options.Create(Config());

        var missing = await ExportEndpointExtensions.DownloadAsync(
            Guid.NewGuid(), new DefaultHttpContext(), store, options, TestContext.Current.CancellationToken);
        var queuedContext = new DefaultHttpContext();
        var accepted = await ExportEndpointExtensions.DownloadAsync(
            queued.ExportId, queuedContext, store, options, TestContext.Current.CancellationToken);
        var rejected = await ExportEndpointExtensions.DownloadAsync(
            failed.ExportId, new DefaultHttpContext(), store, options, TestContext.Current.CancellationToken);
        var gone = await ExportEndpointExtensions.DownloadAsync(
            expired.ExportId, new DefaultHttpContext(), store, options, TestContext.Current.CancellationToken);

        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(missing).StatusCode);
        Assert.Equal(202, Assert.IsAssignableFrom<IStatusCodeHttpResult>(accepted).StatusCode);
        Assert.Equal("5", queuedContext.Response.Headers.RetryAfter);
        Assert.Equal(422, Assert.IsAssignableFrom<IStatusCodeHttpResult>(rejected).StatusCode);
        Assert.Equal(404, Assert.IsAssignableFrom<IStatusCodeHttpResult>(gone).StatusCode);
    }

    [Fact]
    public async Task DownloadEndpoint_CompletedFile_ReturnsFileResult()
    {
        var job = Job(ExportStatus.Completed);
        job.FileName = "customers.csv";
        job.ContentType = "text/csv";
        var store = new FakeStore(root, [job]);
        Directory.CreateDirectory(store.GetJobFolder(job.ExportId));
        await File.WriteAllTextAsync(
            store.GetFilePath(job.ExportId, job.FileName),
            "id\n1\n",
            TestContext.Current.CancellationToken);
        job.FileBytes = new FileInfo(store.GetFilePath(job.ExportId, job.FileName)).Length;

        var result = await ExportEndpointExtensions.DownloadAsync(
            job.ExportId,
            new DefaultHttpContext(),
            store,
            Options.Create(Config()),
            TestContext.Current.CancellationToken);

        var file = Assert.IsAssignableFrom<IFileHttpResult>(result);
        Assert.Equal("customers.csv", file.FileDownloadName);
        Assert.Equal("text/csv", file.ContentType);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private static ServiceProvider Services(IExportGenerator exporter) =>
        new ServiceCollection()
            .AddLogging()
            .AddScoped<IExportGenerator>(_ => exporter)
            .BuildServiceProvider();

    private ExportConfig Config(int maxConcurrent = 2) => new()
    {
        SharedStorageRoot = root,
        MaxConcurrentExports = maxConcurrent,
        WorkerScanIntervalSeconds = 1,
        LeaseSeconds = 60,
        CleanupIntervalMinutes = 1
    };

    private static ExportJobRecord Job(ExportStatus status) => new()
    {
        ExportId = Guid.NewGuid(),
        EntityName = "Customer",
        RequestType = ExportRequestType.ColumnSelector,
        Format = ExportFileFormat.Csv,
        Columns = ["id"],
        RequestedBy = "test",
        RequestedUtc = DateTimeOffset.UtcNow,
        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
        Status = status
    };

    private static ExportJobRecord WithExpiry(ExportJobRecord job, DateTimeOffset expiry)
    {
        job.ExpiresUtc = expiry;
        return job;
    }

    private sealed class FakeExporter(int delayMilliseconds = 0, bool throws = false) : IExportGenerator
    {
        private int active;
        public int MaxActive { get; private set; }
        public string EntityName => "Customer";

        public async Task<GeneratedExport> GenerateAsync(
            ExportJobRecord job,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref active);
            MaxActive = Math.Max(MaxActive, current);
            try
            {
                if (delayMilliseconds > 0)
                    await Task.Delay(delayMilliseconds, cancellationToken);
                if (throws) throw new InvalidOperationException("failure detail");
                return new GeneratedExport("customers.csv", "text/csv", 1, 4);
            }
            finally
            {
                Interlocked.Decrement(ref active);
            }
        }
    }

    private sealed class FakeStore(string root, IEnumerable<ExportJobRecord> jobs) : IExportJobStore
    {
        private readonly ConcurrentDictionary<Guid, ExportJobRecord> records =
            new(jobs.ToDictionary(job => job.ExportId));
        public ConcurrentBag<Guid> Released { get; } = [];
        public ConcurrentBag<Guid> Deleted { get; } = [];
        public Guid? ThrowOnDelete { get; init; }
        public string RootPath => root;
        public Task CreateAsync(ExportJobRecord job, CancellationToken cancellationToken)
        {
            records[job.ExportId] = job;
            return Task.CompletedTask;
        }
        public Task<ExportJobRecord?> ReadAsync(Guid exportId, CancellationToken cancellationToken) =>
            Task.FromResult(records.GetValueOrDefault(exportId));
        public Task WriteAsync(ExportJobRecord job, CancellationToken cancellationToken)
        {
            records[job.ExportId] = job;
            return Task.CompletedTask;
        }
        public async IAsyncEnumerable<Guid> ListAsync(
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var id in records.Keys)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return id;
                await Task.Yield();
            }
        }
        public Task<ExportJobRecord?> TryClaimAsync(Guid exportId, string serverName, CancellationToken cancellationToken)
        {
            var job = records.GetValueOrDefault(exportId);
            if (job?.Status != ExportStatus.Queued) return Task.FromResult<ExportJobRecord?>(null);
            job.Status = ExportStatus.Claimed;
            job.ServerName = serverName;
            return Task.FromResult<ExportJobRecord?>(job);
        }
        public void ReleaseClaim(Guid exportId) => Released.Add(exportId);
        public string GetJobFolder(Guid exportId) => Path.Combine(root, exportId.ToString("D"));
        public string GetFilePath(Guid exportId, string fileName) => Path.Combine(GetJobFolder(exportId), fileName);
        public Task DeleteAsync(Guid exportId, CancellationToken cancellationToken)
        {
            if (ThrowOnDelete == exportId) throw new IOException("delete failed");
            Deleted.Add(exportId);
            records.TryRemove(exportId, out _);
            return Task.CompletedTask;
        }
    }
}
