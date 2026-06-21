# Unit Testing With xUnit

Use xUnit for unit tests. Use mocks for dependencies that cross the unit boundary, such as service contracts, repositories, integration clients, loggers, clocks, and storage abstractions.

## Test Project Structure

Use one test project per production service.

```text
DotnetRestEfService.Tests/
  Controllers/
  Services/
  Middleware/
  IntegrationClients/
  TestData/
  TestDoubles/
```

Recommended packages:

```xml
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="xunit.v3" />
<PackageReference Include="xunit.runner.visualstudio" />
<PackageReference Include="Moq" />
```

Manage package versions through `Directory.Packages.props` or the project package policy.

Rules:

- Test one behavior per test.
- Use Arrange, Act, Assert ordering.
- Use `[Fact]` for a single scenario.
- Use `[Theory]` with `[InlineData]` for multiple input cases.
- Mock interfaces you own.
- Do not mock DTOs, records, simple entities, or value objects.
- Do not mock EF Core query behavior in unit tests. Use integration tests with SQLite for EF query translation and persistence behavior.
- Do not make real HTTP, GraphQL, gRPC, file system, database, or clock calls in unit tests.
- Keep tests deterministic and independent.

## Naming

Use behavior-focused names.

```csharp
public async Task GetByIdAsync_WhenProductExists_ReturnsOkWithProduct()
```

Pattern:

```text
MethodName_WhenCondition_ExpectedResult
```

## Controller Unit Tests

Controllers should be tested by mocking service contracts. Do not use EF Core in controller unit tests.

Good:

```csharp
using DotnetRestEfService.Controllers;
using DotnetRestEfService.DataContracts;
using DotnetRestEfService.Services.Contracts;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

public class ProductsControllerTests
{
    [Fact]
    public async Task GetByIdAsync_WhenProductExists_ReturnsOkWithProduct()
    {
        var product = new ProductDto(
            1,
            "Laptop Stand",
            "Adjustable stand",
            49.99m,
            10,
            DateTime.UtcNow,
            null);

        var productService = new Mock<IProductService>();
        productService
            .Setup(service => service.GetByIdAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(product);

        var controller = new ProductsController(productService.Object);

        var result = await controller.GetByIdAsync(1, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<ProductDto>(okResult.Value);
        Assert.Equal(product.Id, response.Id);
    }
}
```

Bad:

```csharp
[Fact]
public async Task GetByIdAsync_BadExample_UsesRealDatabase()
{
    var dbContext = new AppDbContext(realOptions);
    var controller = new ProductsController(dbContext);

    var result = await controller.GetByIdAsync(1);

    Assert.NotNull(result);
}
```

## Service Unit Tests

Service unit tests should mock dependencies outside the service. If a service depends directly on EF Core, test business rules with focused integration tests instead of mocking `DbContext`.

Good service design for unit testing:

```csharp
public interface ICatalogClient
{
    Task<CatalogItemDto?> GetItemAsync(int id, CancellationToken cancellationToken);
}

public class PricingService(ICatalogClient catalogClient)
{
    public async Task<decimal> GetDiscountedPriceAsync(
        int itemId,
        CancellationToken cancellationToken)
    {
        var item = await catalogClient.GetItemAsync(itemId, cancellationToken)
            ?? throw new NotFoundException($"Catalog item {itemId} was not found.");

        return item.UnitPrice * 0.9m;
    }
}
```

Good test:

```csharp
using Moq;
using Xunit;

public class PricingServiceTests
{
    [Fact]
    public async Task GetDiscountedPriceAsync_WhenItemExists_ReturnsTenPercentDiscount()
    {
        var catalogClient = new Mock<ICatalogClient>();
        catalogClient
            .Setup(client => client.GetItemAsync(10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CatalogItemDto(10, "Keyboard", 100m));

        var service = new PricingService(catalogClient.Object);

        var price = await service.GetDiscountedPriceAsync(10, CancellationToken.None);

        Assert.Equal(90m, price);
    }
}
```

Bad:

```csharp
[Fact]
public async Task GetDiscountedPriceAsync_BadExample_CallsRealCatalogApi()
{
    var client = new CatalogRestClient(new HttpClient());
    var service = new PricingService(client);

    var price = await service.GetDiscountedPriceAsync(10, CancellationToken.None);

    Assert.True(price > 0);
}
```

## Exception Unit Tests

Use `Assert.ThrowsAsync<TException>` for expected exceptions.

```csharp
[Fact]
public async Task GetDiscountedPriceAsync_WhenItemMissing_ThrowsNotFoundException()
{
    var catalogClient = new Mock<ICatalogClient>();
    catalogClient
        .Setup(client => client.GetItemAsync(99, It.IsAny<CancellationToken>()))
        .ReturnsAsync((CatalogItemDto?)null);

    var service = new PricingService(catalogClient.Object);

    var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
        service.GetDiscountedPriceAsync(99, CancellationToken.None));

    Assert.Contains("99", exception.Message);
}
```

## Mock Verification

Verify important interactions, not every method call.

Good:

```csharp
productService.Verify(
    service => service.CreateAsync(request, It.IsAny<CancellationToken>()),
    Times.Once);
```

Bad:

```csharp
productService.VerifyAll();
```

Rules:

- Verify calls only when the call itself is the behavior.
- Avoid `VerifyAll()` because it makes tests brittle.
- Prefer asserting returned values and state over excessive interaction checks.

## Theory Tests

Use `[Theory]` for repeated validation or calculation scenarios.

```csharp
public class ProductSearchQueryTests
{
    [Theory]
    [InlineData(1, 25)]
    [InlineData(2, 50)]
    public void Constructor_WhenValidPagingValues_CreatesQuery(int page, int pageSize)
    {
        var query = new ProductSearchQuery(null, page, pageSize, "name", "asc");

        Assert.Equal(page, query.Page);
        Assert.Equal(pageSize, query.PageSize);
    }
}
```

## Integration Client Unit Tests

For typed REST clients, mock the HTTP boundary with a fake `HttpMessageHandler`. Do not call the real downstream API.

```csharp
public class FakeHttpMessageHandler(HttpResponseMessage response) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        Task.FromResult(response);
}
```

Example:

```csharp
[Fact]
public async Task GetItemAsync_WhenApiReturnsItem_ReturnsCatalogItem()
{
    var response = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new CatalogItemDto(1, "USB-C Hub", 59.99m))
    };

    var httpClient = new HttpClient(new FakeHttpMessageHandler(response))
    {
        BaseAddress = new Uri("https://catalog.test")
    };

    var client = new CatalogRestClient(httpClient);

    var item = await client.GetItemAsync(1, CancellationToken.None);

    Assert.NotNull(item);
    Assert.Equal("USB-C Hub", item.Name);
}
```

## Logger Mocks

Do not assert exact formatted log messages in normal unit tests. Verify behavior and exceptions first. When logging is the behavior, verify that a log call happened at the expected level.

```csharp
var logger = new Mock<ILogger<ExceptionHandlingMiddleware>>();
```

Rules:

- Avoid brittle assertions on exact log text.
- Do not log secrets in test data.
- Use realistic but fake IDs, URLs, and names.

## Coverage Requirements

Coverage is a quality signal, not a replacement for meaningful assertions. Coverage must be reviewed together with test quality.

Minimum thresholds:

| Scope | Required Coverage |
| --- | --- |
| Overall project coverage | `80%` |
| New or changed code coverage | `90%` |

Rules:

- Overall line coverage must be at least `80%`.
- New or changed code line coverage must be at least `90%`.
- Do not lower coverage thresholds to merge a change.
- Do not add low-value tests that only execute lines without asserting behavior.
- Exclude generated code, migrations, designer files, and simple configuration bootstrap code when the project policy allows it.
- Cover business rules, exception paths, validation paths, and important edge cases.
- Use integration tests for EF Core persistence and query translation coverage.

Recommended coverage command:

```bash
dotnet test \
  --collect:"XPlat Code Coverage" \
  --settings coverlet.runsettings
```

Example `coverlet.runsettings`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <DataCollectionRunSettings>
    <DataCollectors>
      <DataCollector friendlyName="XPlat Code Coverage">
        <Configuration>
          <Format>cobertura</Format>
          <ExcludeByFile>**/Migrations/*.cs</ExcludeByFile>
          <ExcludeByAttribute>GeneratedCodeAttribute,CompilerGeneratedAttribute</ExcludeByAttribute>
        </Configuration>
      </DataCollector>
    </DataCollectors>
  </DataCollectionRunSettings>
</RunSettings>
```

Coverage review rules:

- Review uncovered new lines before approving a pull request.
- Require explicit justification for uncovered new code.
- Prefer branch and edge-case coverage for business services over shallow controller-only coverage.
- Treat missing tests for negative paths as a review finding even when numeric coverage passes.

## Testing Scope And Coverage

Recommended tests:

- Controller tests for routing and response behavior.
- Service tests for business rules and exception cases.
- Integration tests for CRUD endpoints.
- Transaction tests for rollback behavior.
- Validation tests for bad request payloads.
- Exception middleware tests for `ProblemDetails` responses.
- Integration client tests using mocked HTTP, GraphQL, gRPC, or SDK responses.

Minimum CRUD coverage:

- Create succeeds.
- Get all succeeds.
- Get by ID succeeds.
- Get by missing ID returns `404`.
- Update succeeds and commits.
- Update missing ID returns `404`.
- Delete succeeds.
- Delete missing ID returns `404`.
- Duplicate unique value returns `409`.

## Unit Test Checklist

Before accepting unit tests, verify:

- Tests use xUnit.
- Dependencies are mocked through interfaces.
- Tests do not call real databases, HTTP APIs, file systems, queues, clocks, or secrets.
- Tests follow Arrange, Act, Assert.
- Test names describe behavior.
- Assertions verify outputs, exceptions, or meaningful interactions.
- Mocks are not over-verified.
- EF Core query behavior is covered by integration tests, not mocked unit tests.
- Overall project coverage is at least `80%`.
- New or changed code coverage is at least `90%`.
- Uncovered new code has a documented reason.
