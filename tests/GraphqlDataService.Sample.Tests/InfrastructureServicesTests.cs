using System.Text.Json;
using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.Data.Entities;
using GraphqlDataService.Sample.GraphQL;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Grid;
using GraphqlDataService.Sample.Health;
using HotChocolate;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.Tests;

public sealed class InfrastructureServicesTests : IDisposable
{
    private readonly string root = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        $"graphql-infrastructure-{Guid.NewGuid():N}");

    [Fact]
    public async Task DatabaseInitializer_CreatesSeedOnce()
    {
        var factory = TestDb.Factory();
        var initializer = new DatabaseInitializer(factory, [new GridSeed()]);

        await initializer.InitializeAsync(TestContext.Current.CancellationToken);
        await initializer.InitializeAsync(TestContext.Current.CancellationToken);

        await using var db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        var row = Assert.Single(db.GridSchemas);
        Assert.Equal("Customer", row.EntityName);
        Assert.Equal("sample-seed", row.CreatedBy);
    }

    [Fact]
    public async Task GridRegistry_ReturnsCaseInsensitiveDefinition()
    {
        var factory = TestDb.Factory();
        var definition = Definition();
        await using (var db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            db.GridSchemas.Add(Row(JsonSerializer.Serialize(definition)));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var registry = new EntityGridDefinitionRegistry(factory);

        var loaded = await registry.GetRequiredAsync(
            "customer",
            "DEFAULT",
            TestContext.Current.CancellationToken);
        var throughQuery = await new GridDefinitionQuery().GetGridDefinitionAsync(
            registry,
            "Customer",
            "default",
            TestContext.Current.CancellationToken);

        Assert.Equal("Customer", loaded.EntityName);
        Assert.Equal(loaded.EntityName, throughQuery.EntityName);
        Assert.Equal(loaded.GridViewName, throughQuery.GridViewName);
        Assert.Equal(loaded.Columns, throughQuery.Columns);
    }

    [Fact]
    public async Task GridRegistry_MissingOrInvalid_ReturnsStableErrors()
    {
        var missingRegistry = new EntityGridDefinitionRegistry(TestDb.Factory());
        var missing = await Assert.ThrowsAsync<GraphQLException>(() =>
            missingRegistry.GetRequiredAsync(
                "Customer",
                "default",
                TestContext.Current.CancellationToken));

        var factory = TestDb.Factory();
        await using (var db = await factory.CreateDbContextAsync(TestContext.Current.CancellationToken))
        {
            db.GridSchemas.Add(Row("not-json"));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        var invalid = await Assert.ThrowsAsync<GraphQLException>(() =>
            new EntityGridDefinitionRegistry(factory).GetRequiredAsync(
                "Customer",
                "default",
                TestContext.Current.CancellationToken));

        Assert.Equal("GRID_DEFINITION_NOT_FOUND", Assert.Single(missing.Errors).Code);
        Assert.Equal("GRID_DEFINITION_INVALID", Assert.Single(invalid.Errors).Code);
    }

    [Fact]
    public void SanitizingErrorFilter_HandlesExpectedProductionAndDevelopmentErrors()
    {
        var expected = ErrorBuilder.New().SetMessage("expected").Build();
        var exception = new InvalidOperationException("database detail");
        var unexpected = ErrorBuilder.New()
            .SetMessage("database detail")
            .SetException(exception)
            .Build();
        var production = new SanitizingErrorFilter(
            NullLogger<SanitizingErrorFilter>.Instance,
            Options.Create(new GraphQlConfig { IncludeExceptionDetails = false }));
        var development = new SanitizingErrorFilter(
            NullLogger<SanitizingErrorFilter>.Instance,
            Options.Create(new GraphQlConfig { IncludeExceptionDetails = true }));

        var unchanged = production.OnError(expected);
        var sanitized = production.OnError(unexpected);
        var detailed = development.OnError(unexpected);

        Assert.Same(expected, unchanged);
        Assert.Equal("An internal error occurred.", sanitized.Message);
        Assert.Equal("INTERNAL_SERVER_ERROR", sanitized.Code);
        Assert.Null(sanitized.Exception);
        Assert.Equal("database detail", detailed.Message);
        Assert.Equal("INTERNAL_SERVER_ERROR", detailed.Code);
        Assert.Same(exception, detailed.Exception);
    }

    [Fact]
    public async Task ExportStorageHealthCheck_ReturnsHealthyForWritableRoot()
    {
        var store = Store();
        var check = new ExportStorageHealthCheck(store);

        var result = await check.CheckHealthAsync(
            new HealthCheckContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private YamlExportJobStore Store() => new(Options.Create(new ExportConfig
    {
        SharedStorageRoot = root,
        LeaseSeconds = 60,
        WorkerScanIntervalSeconds = 1
    }));

    private static GridDefinition Definition() => new(
        "Customer",
        "default",
        ExportFileFormat.Csv,
        [new("Name", GridColumnType.String, null, TextAlignment.Left, true, 100, true, true, "name")]);

    private static GridSchema Row(string json) => new()
    {
        Id = Guid.NewGuid(),
        EntityName = "Customer",
        ViewName = "default",
        Definition = json,
        Version = 1,
        IsActive = true,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private sealed class GridSeed : IEntityGridDefinition
    {
        public string EntityName => "Customer";
        public string GridViewName => "default";
        public ExportFileFormat? ExportFormat => ExportFileFormat.Csv;
        public IReadOnlyList<GridColumnDefinition> Columns => Definition().Columns;
    }
}
