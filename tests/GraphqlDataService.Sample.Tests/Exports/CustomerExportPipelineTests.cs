using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.Provider.Customer;
using Microsoft.Extensions.Options;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Tests.Exports;

public sealed class CustomerExportPipelineTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"graphql-export-pipeline-{Guid.NewGuid():N}");

    [Fact]
    public async Task GenerateAsync_Csv_AppliesFilterOrderProjectionAndEscaping()
    {
        var database = Guid.NewGuid().ToString("N");
        var factory = TestDb.Factory(database);
        await SeedAsync(factory,
            Customer("=Formula", "formula@example.test"),
            Customer("Ada, \"Countess\"", "ada@example.test"),
            Customer("Other", "other@example.test"));
        var store = Store();
        var job = Job(ExportFileFormat.Csv, ["name", "email"]);
        job.Filters = [new ExportFilterInput
        {
            Field = "name",
            Operator = ExportFilterOperator.Contains,
            Value = "a"
        }];
        job.Order = [new ExportOrderInput
        {
            Field = "name",
            Direction = ExportSortDirection.Descending
        }];
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        var output = await Generator(factory, store).GenerateAsync(
            job,
            TestContext.Current.CancellationToken);
        var csv = await File.ReadAllTextAsync(
            store.GetFilePath(job.ExportId, output.FileName),
            TestContext.Current.CancellationToken);

        Assert.Equal("customers.csv", output.FileName);
        Assert.Equal("text/csv; charset=utf-8", output.ContentType);
        Assert.Equal(2, output.RowCount);
        Assert.Contains("name,email", csv);
        Assert.Contains("'=Formula", csv);
        Assert.Contains("\"Ada, \"\"Countess\"\"\"", csv);
        Assert.DoesNotContain("other@example.test", csv);
    }

    [Fact]
    public async Task GenerateAsync_Excel_WritesWorkbook()
    {
        var factory = TestDb.Factory();
        await SeedAsync(factory, Customer("Ada", "ada@example.test"));
        var store = Store();
        var job = Job(ExportFileFormat.Excel, ["id", "birthDate"]);
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        var output = await Generator(factory, store).GenerateAsync(
            job,
            TestContext.Current.CancellationToken);
        var bytes = await File.ReadAllBytesAsync(
            store.GetFilePath(job.ExportId, output.FileName),
            TestContext.Current.CancellationToken);

        Assert.Equal("customers.xlsx", output.FileName);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", output.ContentType);
        Assert.True(bytes.Length > 4);
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);
    }

    [Fact]
    public async Task GenerateAsync_WhenRowLimitExceeded_ThrowsStableError()
    {
        var factory = TestDb.Factory();
        await SeedAsync(factory,
            Customer("One", "one@example.test"),
            Customer("Two", "two@example.test"));
        var store = Store();
        var job = Job(ExportFileFormat.Csv, ["id"]);

        var exception = await Assert.ThrowsAsync<ExportGenerationException>(() =>
            Generator(factory, store, maxRows: 1).GenerateAsync(
                job,
                TestContext.Current.CancellationToken));

        Assert.Equal("EXPORT_LIMIT_EXCEEDED", exception.Code);
    }

    [Fact]
    public async Task GenerateAsync_WhenByteLimitExceeded_RemovesPartialFile()
    {
        var factory = TestDb.Factory();
        await SeedAsync(factory, Customer("Ada", "ada@example.test"));
        var store = Store();
        var job = Job(ExportFileFormat.Csv, ["name"]);
        await store.CreateAsync(job, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<ExportGenerationException>(() =>
            Generator(factory, store, maxBytes: 1).GenerateAsync(
                job,
                TestContext.Current.CancellationToken));

        Assert.Equal("EXPORT_LIMIT_EXCEEDED", exception.Code);
        Assert.False(File.Exists(store.GetFilePath(job.ExportId, "customers.csv")));
    }

    [Fact]
    public async Task GenerateAsync_InvalidColumn_ThrowsStableError()
    {
        var factory = TestDb.Factory();
        await SeedAsync(factory, Customer("Ada", "ada@example.test"));
        var store = Store();
        var job = Job(ExportFileFormat.Csv, ["unknown"]);

        var exception = await Assert.ThrowsAsync<ExportGenerationException>(() =>
            Generator(factory, store).GenerateAsync(job, TestContext.Current.CancellationToken));

        Assert.Equal("EXPORT_COLUMN_INVALID", exception.Code);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private CustomerExportGenerator Generator(
        Microsoft.EntityFrameworkCore.IDbContextFactory<GraphqlDataService.Sample.Data.AppDbContext> factory,
        IExportJobStore store,
        int maxRows = 100,
        long maxBytes = 1_000_000) =>
        new(
            factory,
            store,
            new CustomerQueryCapabilities(),
            Options.Create(new ExportConfig
            {
                SharedStorageRoot = root,
                MaxRows = maxRows,
                MaxBytes = maxBytes
            }));

    private YamlExportJobStore Store() => new(Options.Create(new ExportConfig
    {
        SharedStorageRoot = root,
        LeaseSeconds = 60,
        WorkerScanIntervalSeconds = 1,
        MaxAttempts = 3
    }));

    private static ExportJobRecord Job(ExportFileFormat format, IReadOnlyList<string> columns) => new()
    {
        ExportId = Guid.NewGuid(),
        EntityName = "Customer",
        RequestType = ExportRequestType.ColumnSelector,
        Format = format,
        Columns = [.. columns],
        RequestedBy = "test",
        RequestedUtc = DateTimeOffset.UtcNow,
        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(5),
        Status = ExportStatus.Queued
    };

    private static CustomerEntity Customer(string name, string email) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Email = email,
        BirthDate = new DateOnly(2000, 1, 2),
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task SeedAsync(
        Microsoft.EntityFrameworkCore.IDbContextFactory<GraphqlDataService.Sample.Data.AppDbContext> factory,
        params CustomerEntity[] customers)
    {
        await using var db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        db.AddRange(customers);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
