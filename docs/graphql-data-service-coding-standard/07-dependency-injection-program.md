# Dependency Injection And Program.cs

`Program.cs` registers infrastructure; `GraphQlConfiguration` owns schema configuration and provider discovery.

The service uses `AddDbContextFactory<AppDbContext>` and also provides a scoped context created from that factory. GraphQL registers the factory with `RegisterDbContextFactory<AppDbContext>()`.

`GraphQL:ProviderNamespacePrefix` controls discovery of concrete provider classes. Beneath that namespace:

- classes with `[ExtendObjectType]` are added as GraphQL type extensions;
- `ICrudMapper<,,,>` implementations are registered scoped, both concretely and by contract;
- `IEntityExportQueryCapabilities<>` implementations are registered singleton;
- `IExportGenerator` implementations are registered scoped and enumerable.

`Database:ProviderNamespacePrefix` separately controls EF configuration discovery in `AppDbContext.ApplyConfigurationsFromAssembly`.

The GraphQL roots are configured by name:

```csharp
.AddQueryType(descriptor => descriptor.Name(OperationTypeNames.Query))
.AddMutationType(descriptor => descriptor.Name(OperationTypeNames.Mutation))
.AddSubscriptionType(descriptor => descriptor.Name(OperationTypeNames.Subscription))
```

There are no `Query.cs` or `Mutation.cs` marker classes and no manual Customer registration in `Program.cs`.

The current middleware is routing, Serilog request logging, WebSockets/GraphQL endpoint mapping, export REST mapping, and health endpoints. Authentication/authorization middleware is absent because the sample has auth disabled.
