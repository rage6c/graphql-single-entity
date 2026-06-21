using System.ComponentModel.DataAnnotations;

namespace GraphqlDataService.Sample.Configuration;

public sealed class GraphQlConfig
{
    public const string SectionName = "GraphQL";

    [Required]
    public string ProviderNamespacePrefix { get; init; } =
        "GraphqlDataService.Sample.Provider";

    [Range(1, 10000)]
    public int DefaultPageSize { get; init; } = 25;

    [Range(1, 5000)]
    public int MaxPageSize { get; init; } = 100;

    [Range(1, 30)]
    public int MaxExecutionDepth { get; init; } = 10;

    public bool IncludeExceptionDetails { get; init; }
}
