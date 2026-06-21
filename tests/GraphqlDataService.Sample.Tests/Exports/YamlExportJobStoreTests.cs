using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.Provider.Customer;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.Tests.Exports;

public sealed class YamlExportJobStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"graphql-export-tests-{Guid.NewGuid():N}");

    [Fact]
    public async Task CreateAsync_WritesVersionedYamlInsideUuidFolder()
    {
        var store = CreateStore();
        var job = CreateJob();

        await store.CreateAsync(job, TestContext.Current.CancellationToken);
        var loaded = await store.ReadAsync(job.ExportId, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(job.ExportId, loaded.ExportId);
        Assert.Equal(ExportStatus.Queued, loaded.Status);
        Assert.Equal("name", Assert.Single(loaded.Filters).Field);
        Assert.Equal("email", Assert.Single(loaded.Order).Field);
        Assert.True(File.Exists(Path.Combine(_root, job.ExportId.ToString("D"), "job.yaml")));
    }

    [Fact]
    public async Task TryClaimAsync_AllowsOnlyOneServerToOwnAJob()
    {
        var store = CreateStore();
        var job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        var first = await store.TryClaimAsync(
            job.ExportId,
            "server-a",
            TestContext.Current.CancellationToken);
        var second = await store.TryClaimAsync(
            job.ExportId,
            "server-b",
            TestContext.Current.CancellationToken);

        Assert.NotNull(first);
        Assert.Equal("server-a", first.ServerName);
        Assert.Null(second);
    }

    [Fact]
    public async Task ListReleaseAndDelete_ManageJobFolders()
    {
        var store = CreateStore();
        var job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);
        Directory.CreateDirectory(Path.Combine(_root, "not-a-guid"));
        Assert.Null(await store.ReadAsync(Guid.NewGuid(), TestContext.Current.CancellationToken));
        var claimed = await store.TryClaimAsync(
            job.ExportId,
            "server",
            TestContext.Current.CancellationToken);

        var listed = new List<Guid>();
        await foreach (var id in store.ListAsync(TestContext.Current.CancellationToken)) listed.Add(id);
        store.ReleaseClaim(job.ExportId);
        await store.DeleteAsync(job.ExportId, TestContext.Current.CancellationToken);
        await store.DeleteAsync(job.ExportId, TestContext.Current.CancellationToken);

        Assert.NotNull(claimed);
        Assert.Equal([job.ExportId], listed);
        Assert.False(Directory.Exists(store.GetJobFolder(job.ExportId)));
    }

    [Fact]
    public async Task TryClaimAsync_RecoversExpiredLease()
    {
        var store = CreateStore();
        var job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);
        var first = await store.TryClaimAsync(
            job.ExportId,
            "server-a",
            TestContext.Current.CancellationToken);
        Assert.NotNull(first);
        first.Status = ExportStatus.Running;
        first.LeaseUntilUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        await store.WriteAsync(first, TestContext.Current.CancellationToken);
        var lockPath = Path.Combine(store.GetJobFolder(job.ExportId), "claim.lock");
        File.SetLastWriteTimeUtc(lockPath, DateTime.UtcNow.AddMinutes(-2));

        var recovered = await store.TryClaimAsync(
            job.ExportId,
            "server-b",
            TestContext.Current.CancellationToken);

        Assert.NotNull(recovered);
        Assert.Equal("server-b", recovered.ServerName);
        Assert.Equal(2, recovered.Attempt);
        store.ReleaseClaim(job.ExportId);
    }

    [Fact]
    public async Task DownloadCustomersStatus_WhenJobCompletes_EmitsDownloadUrl()
    {
        var store = CreateStore();
        var job = CreateJob();
        await store.CreateAsync(job, TestContext.Current.CancellationToken);
        var subscription = new CustomerSubscription();
        var subscriptionOptions = Options.Create(new ExportConfig
        {
            SharedStorageRoot = _root,
            StatusPollingIntervalSeconds = 0
        });

        await using var updates = subscription.SubscribeToDownloadCustomersStatus(
                job.ExportId,
                store,
                subscriptionOptions,
                TestContext.Current.CancellationToken)
            .GetAsyncEnumerator(TestContext.Current.CancellationToken);

        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(ExportStatus.Queued, updates.Current.Status);
        Assert.Null(updates.Current.DownloadUrl);

        job.Status = ExportStatus.Completed;
        await store.WriteAsync(job, TestContext.Current.CancellationToken);

        Assert.True(await updates.MoveNextAsync());
        Assert.Equal(ExportStatus.Completed, updates.Current.Status);
        Assert.Equal($"/exports/{job.ExportId:D}/download", updates.Current.DownloadUrl);
        Assert.False(await updates.MoveNextAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private YamlExportJobStore CreateStore() => new(Options.Create(new ExportConfig
    {
        SharedStorageRoot = _root,
        LeaseSeconds = 60,
        WorkerScanIntervalSeconds = 1,
        MaxAttempts = 3
    }));

    private static ExportJobRecord CreateJob() => new()
    {
        ExportId = Guid.NewGuid(),
        EntityName = "Customer",
        RequestType = ExportRequestType.ColumnSelector,
        Format = ExportFileFormat.Csv,
        Columns = ["id", "name"],
        Filters =
        [
            new ExportFilterInput
            {
                Field = "name",
                Operator = ExportFilterOperator.Contains,
                Value = "Customer"
            }
        ],
        Order =
        [
            new ExportOrderInput
            {
                Field = "email",
                Direction = ExportSortDirection.Descending
            }
        ],
        RequestedBy = "test-user",
        RequestedUtc = DateTimeOffset.UtcNow,
        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(30),
        Status = ExportStatus.Queued
    };
}
