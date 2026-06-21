namespace GraphqlDataService.Sample.GraphQL;

public sealed class SanitizingErrorFilter(
    ILogger<SanitizingErrorFilter> logger) : IErrorFilter
{
    public IError OnError(IError error)
    {
        if (error.Exception is null)
        {
            return error;
        }

        logger.LogError(error.Exception, "Unexpected GraphQL execution error");
        return error
            .WithMessage("An internal error occurred.")
            .WithCode("INTERNAL_SERVER_ERROR")
            .WithException(null);
    }
}
