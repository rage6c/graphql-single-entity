using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.GraphQL.Export;
using GraphqlDataService.Sample.GraphQL.Grid;
using GraphqlDataService.Sample.GraphQL.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace GraphqlDataService.Sample.GraphQL;

public static class GraphQlConfiguration
{
    public static IServiceCollection AddApplicationGraphQl(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<GraphQlConfig>()
            .Bind(configuration.GetSection(GraphQlConfig.SectionName))
            .ValidateDataAnnotations()
            .Validate(config => config.DefaultPageSize <= config.MaxPageSize)
            .ValidateOnStart();

        var config = configuration
            .GetSection(GraphQlConfig.SectionName)
            .Get<GraphQlConfig>() ?? new GraphQlConfig();

        var providerTypes = DiscoverProviderTypes(config.ProviderNamespacePrefix);
        if (providerTypes.Count == 0)
        {
            throw new InvalidOperationException(
                $"No provider types were found under namespace '{config.ProviderNamespacePrefix}'. " +
                "Check GraphQL:ProviderNamespacePrefix in configuration.");
        }

        services.AddProviderServices(providerTypes);

        var graphQl = services
            .AddGraphQLServer()
            .AddQueryType(descriptor => descriptor.Name(OperationTypeNames.Query))
            .AddMutationType(descriptor => descriptor.Name(OperationTypeNames.Mutation))
            .AddSubscriptionType(descriptor => descriptor.Name(OperationTypeNames.Subscription))
            .AddTypeExtension<GridDefinitionQuery>()
            .RegisterDbContextFactory<AppDbContext>()
            .AddProjections()
            .AddFiltering()
            .AddSorting()
            .AddInMemorySubscriptions()
            .AddErrorFilter<SanitizingErrorFilter>()
            .ModifyRequestOptions(options =>
                options.IncludeExceptionDetails = config.IncludeExceptionDetails)
            .ModifyPagingOptions(options =>
            {
                options.DefaultPageSize = config.DefaultPageSize;
                options.MaxPageSize = config.MaxPageSize;
            });

        foreach (var providerType in providerTypes.Where(IsGraphQlTypeExtension))
        {
            graphQl.AddTypeExtension(providerType);
        }

        return services;
    }

    public static WebApplication UseApplicationGraphQl(this WebApplication app)
    {
        app.UseWebSockets();
        app.MapGraphQL();
        return app;
    }

    private static IReadOnlyList<Type> DiscoverProviderTypes(string configuredNamespacePrefix)
    {
        var providerNamespace = configuredNamespacePrefix.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(providerNamespace))
        {
            throw new InvalidOperationException(
                "GraphQL:ProviderNamespacePrefix must contain a namespace.");
        }

        return typeof(GraphQlConfiguration).Assembly
            .GetTypes()
            .Where(type =>
                type is { IsClass: true, IsAbstract: false } &&
                type.Namespace is not null &&
                (type.Namespace == providerNamespace ||
                 type.Namespace.StartsWith(providerNamespace + ".", StringComparison.Ordinal)))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsGraphQlTypeExtension(Type type) =>
        type.IsDefined(typeof(ExtendObjectTypeAttribute), inherit: false);

    private static void AddProviderServices(
        this IServiceCollection services,
        IReadOnlyList<Type> providerTypes)
    {
        foreach (var implementationType in providerTypes)
        {
            var contracts = implementationType.GetInterfaces();
            var mapperContracts = contracts.Where(IsCrudMapperContract).ToArray();
            if (mapperContracts.Length > 0)
            {
                services.TryAddScoped(implementationType);
                foreach (var contract in mapperContracts)
                {
                    services.TryAddScoped(
                        contract,
                        provider => provider.GetRequiredService(implementationType));
                }
            }

            foreach (var contract in contracts.Where(IsExportCapabilitiesContract))
            {
                services.TryAddSingleton(contract, implementationType);
            }

            if (typeof(IExportGenerator).IsAssignableFrom(implementationType))
            {
                services.TryAddEnumerable(
                    ServiceDescriptor.Scoped(typeof(IExportGenerator), implementationType));
            }
        }
    }

    private static bool IsCrudMapperContract(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICrudMapper<,,,>);

    private static bool IsExportCapabilitiesContract(Type type) =>
        type.IsGenericType &&
        type.GetGenericTypeDefinition() == typeof(IEntityExportQueryCapabilities<>);
}
