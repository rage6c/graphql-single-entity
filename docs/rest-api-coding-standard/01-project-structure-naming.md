# Project Structure And Naming

## Project Structure

Use a layered structure with clear ownership boundaries.

```text
Controllers/             HTTP endpoints only
DataContracts/           Request and response DTOs
Services/Contracts/      Service interfaces
Services/                Business logic
IntegrationClients/      Outbound clients for REST, GraphQL, gRPC, and other APIs
Data/                    EF Core DbContext, seeding, data access helpers
Data/Entities/           EF Core entities
Exceptions/              Custom application exceptions
Middleware/              Cross-cutting middleware
```

Rules:

- Keep `Program.cs` as the composition root for startup wiring only. See [09-program-cs.md](09-program-cs.md).
- Controllers must not contain business logic or direct EF Core queries.
- Services own business rules, transactions, validation that requires data access, and DTO mapping.
- Integration clients own outbound calls to other services and must hide transport details from business services.
- Data contracts are API-facing models and must not be EF entities.
- EF entities stay inside the data layer and should not be returned directly from controllers.
- Middleware owns cross-cutting concerns such as exception handling.

## Naming Conventions

Use standard .NET naming conventions.

| Item | Standard | Example |
| --- | --- | --- |
| Classes | PascalCase | `ProductService` |
| Interfaces | Prefix with `I` | `IProductService` |
| Methods | PascalCase | `GetByIdAsync` |
| Async methods | End with `Async` | `CreateAsync` |
| Local variables | camelCase | `productName` |
| Private fields | `_camelCase` | `_logger` |
| DTO requests | Verb + resource + `Request` | `CreateProductRequest` |
| DTO responses | Resource + `Dto` | `ProductDto` |
| Exceptions | Meaning + `Exception` | `NotFoundException` |
