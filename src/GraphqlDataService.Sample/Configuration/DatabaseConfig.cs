using System.ComponentModel.DataAnnotations;

namespace GraphqlDataService.Sample.Configuration;

public sealed class DatabaseConfig
{
    public const string SectionName = "Database";

    [Required]
    public string ProviderNamespacePrefix { get; init; } =
        "GraphqlDataService.Sample.Provider";
}
