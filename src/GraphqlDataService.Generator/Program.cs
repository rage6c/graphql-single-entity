using System.ComponentModel.DataAnnotations;
using GraphqlDataService.Generator;
using Microsoft.Extensions.Configuration;

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();
var options = configuration
    .GetSection(GeneratorOptions.SectionName)
    .Get<GeneratorOptions>() ?? new GeneratorOptions();
Validator.ValidateObject(options, new ValidationContext(options), validateAllProperties: true);

var provider = options.Provider.Trim().ToLowerInvariant() switch
{
    "postgresql" or "postgres" => DatabaseProvider.PostgreSql,
    "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
    _ => throw new InvalidOperationException(
        "Generator:Provider must be PostgreSql/Postgres or SqlServer/Mssql.")
};
if (string.IsNullOrWhiteSpace(options.Namespace.Trim().TrimEnd('.')))
{
    throw new InvalidOperationException("Generator:Namespace must contain a namespace.");
}

var outputPath = Path.GetFullPath(options.OutputPath, Environment.CurrentDirectory);
var templates = Path.Combine(AppContext.BaseDirectory, "Templates");
await using var db = new SchemaDbContext(provider, options.ConnectionString);
var tables = await new DatabaseSchemaReader(provider).ReadAsync(
    db,
    options.Schema,
    options.Tables,
    CancellationToken.None);
if (tables.Count == 0)
{
    throw new InvalidOperationException(
        $"No tables were found in schema '{options.Schema}'.");
}

var factory = new EntityModelFactory(provider, options.Namespace.Trim().TrimEnd('.'));
var generator = new SourceGenerator(templates);
foreach (var table in tables)
{
    var model = factory.Create(table);
    await generator.GenerateAsync(model, outputPath, CancellationToken.None);
    Console.WriteLine($"Generated {table.Schema}.{table.Name} -> {Path.Combine(outputPath, model.EntityName)}");
}

[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
internal partial class Program;
