using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Grid;
using GraphqlDataService.Sample.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        "logs/graphql-data-.log",
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024,
        rollOnFileSizeLimit: true,
        retainedFileCountLimit: 50));

builder.Services.AddOptions<ExportConfig>()
    .BindConfiguration(ExportConfig.SectionName)
    .ValidateDataAnnotations()
    .Validate(config => config.LeaseSeconds >= config.WorkerScanIntervalSeconds * 3)
    .ValidateOnStart();
builder.Services.AddOptions<DatabaseConfig>()
    .BindConfiguration(DatabaseConfig.SectionName)
    .ValidateDataAnnotations()
    .Validate(config => !string.IsNullOrWhiteSpace(config.ProviderNamespacePrefix))
    .ValidateOnStart();

var connectionString = builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings:Database is required.");
builder.Services.AddDbContextFactory<AppDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddScoped(provider =>
    provider.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

builder.Services.AddSingleton<IEntityGridDefinitionRegistry, EntityGridDefinitionRegistry>();
builder.Services.AddScoped<DatabaseInitializer>();

builder.Services.AddScoped(typeof(IEntityExportJobService<>), typeof(EntityExportJobService<>));
builder.Services.AddSingleton<IExportJobStore, YamlExportJobStore>();
builder.Services.AddHostedService<ExportJobWorker>();
builder.Services.AddHostedService<ExportCleanupService>();

builder.Services.AddApplicationGraphQl(builder.Configuration);

builder.Services.AddHealthChecks()
    .AddCheck<ExportStorageHealthCheck>("Shared Export Storage", tags: ["ready"]);

var app = builder.Build();

if (!app.Environment.IsEnvironment("Testing"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>()
        .InitializeAsync(CancellationToken.None);
}

app.UseSerilogRequestLogging();
app.UseRouting();
app.UseApplicationGraphQl();
app.MapExportDownloads();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

await app.RunAsync();

public partial class Program;
