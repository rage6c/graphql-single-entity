using System.ComponentModel.DataAnnotations;

namespace GraphqlDataService.Generator;

public sealed class GeneratorOptions
{
    public const string SectionName = "Generator";

    [Required]
    public string Provider { get; init; } = "PostgreSql";

    [Required]
    public string ConnectionString { get; init; } = string.Empty;

    [Required]
    public string Namespace { get; init; } = string.Empty;

    [Required]
    public string OutputPath { get; init; } = string.Empty;

    [Required]
    public string Schema { get; init; } = string.Empty;

    public IReadOnlyList<string> Tables { get; init; } = [];
}

public enum DatabaseProvider
{
    PostgreSql,
    SqlServer
}
