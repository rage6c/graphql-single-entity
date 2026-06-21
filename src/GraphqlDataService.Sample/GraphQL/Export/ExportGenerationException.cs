namespace GraphqlDataService.Sample.GraphQL.Export;

public sealed class ExportGenerationException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
