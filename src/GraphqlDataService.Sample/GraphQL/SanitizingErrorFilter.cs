using GraphqlDataService.Sample.Configuration;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.GraphQL;

public sealed class SanitizingErrorFilter(
    ILogger<SanitizingErrorFilter> logger,
    IOptions<GraphQlConfig> graphQlOptions) : IErrorFilter
{
    private readonly GraphQlConfig _config = graphQlOptions.Value;

    public IError OnError(IError error)
    {
        if (error.Exception is null)
        {
            return error;
        }

        if (_config.IncludeExceptionDetails)
        {
            logger.LogWarning(error.Exception, "GraphQL execution error (details visible)");
            return error.WithCode("INTERNAL_SERVER_ERROR");
        }

        logger.LogError(error.Exception, "Unexpected GraphQL execution error");
        return error
            .WithMessage("An internal error occurred.")
            .WithCode("INTERNAL_SERVER_ERROR")
            .WithException(null);
    }
}
