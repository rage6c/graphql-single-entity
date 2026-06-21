# .NET And Java REST API Comparison

This comparison maps this repository's ASP.NET Core and EF Core standard to a Java baseline using Spring Boot, Spring MVC, Spring Data JPA with Hibernate, JUnit Jupiter, Mockito, Maven or Gradle, SLF4J, and Logback.

The architectural rules should remain consistent across both stacks. Framework-specific mechanics should use the idioms of the target platform rather than literal translations.

## Technology Mapping

| Concern | .NET Standard | Java Equivalent |
| --- | --- | --- |
| Web framework | ASP.NET Core controllers | Spring MVC `@RestController` |
| Data access | EF Core `DbContext` | Spring Data JPA repositories and JPA `EntityManager` |
| ORM provider | EF Core provider | Hibernate is the common JPA implementation |
| Request/response models | C# records or DTO classes | Java records or DTO classes |
| Business services | Scoped service behind an interface | `@Service`, usually a singleton bean behind an interface where useful |
| Dependency injection | Built-in .NET DI | Spring container and constructor injection |
| Configuration | Typed `Config` objects or options | Validated `@ConfigurationProperties` records/classes |
| HTTP client | Typed `HttpClient` from `IHttpClientFactory` | Typed wrapper using `RestClient.Builder` or `WebClient.Builder` |
| Errors | ASP.NET Core `ProblemDetails` | Spring `ProblemDetail`, `ErrorResponse`, and `@RestControllerAdvice` |
| Validation | Data annotations | Jakarta Bean Validation |
| Transactions | EF Core transaction APIs | Service-boundary `@Transactional` |
| Logging | `ILogger<T>` and Serilog | SLF4J and Logback |
| Unit testing | xUnit v3 and Moq | JUnit Jupiter and Mockito |
| Integration testing | Test host and a real test database | `@SpringBootTest`, test slices, and Testcontainers |
| Coverage | Coverlet | JaCoCo |
| API documentation | ASP.NET Core OpenAPI | springdoc-openapi is a common integration |
| Health checks | ASP.NET Core health checks | Spring Boot Actuator health indicators and groups |
| Package management | NuGet | Maven or Gradle |

## Layering And Naming

Use the same ownership boundaries in both stacks.

| Layer | .NET | Java |
| --- | --- | --- |
| HTTP | `Controllers/` | `controller/` |
| API contracts | `DataContracts/` | `dto/` or `api/model/` |
| Service contracts | `Services/Contracts/` | `service/` interfaces where a contract adds value |
| Business logic | `Services/` | `service/` implementations annotated with `@Service` |
| Persistence | `Data/`, `Data/Entities/` | `repository/`, `entity/`, and persistence configuration |
| Outbound clients | `IntegrationClients/` | `client/` or `integration/` |
| Exceptions | `Exceptions/` | `exception/` |
| Cross-cutting HTTP behavior | `Middleware/` | servlet filters, interceptors, or `@RestControllerAdvice` |

Rules that remain the same:

- Keep controllers thin.
- Keep API DTOs separate from persistence entities.
- Keep business rules and transaction boundaries in services.
- Do not expose `IQueryable`, JPA entities, repositories, or transport-specific client types through service contracts.
- Keep outbound transport details inside integration clients.

Naming differences:

- Java types use `PascalCase` and methods/fields use `camelCase`, like .NET types and local variables.
- Do not prefix Java interfaces with `I`; use names such as `ProductService` and `DefaultProductService` only when multiple implementations or a stable boundary justify the interface.
- Java asynchronous methods do not conventionally use an `Async` suffix.

## Configuration

| .NET | Java |
| --- | --- |
| `appsettings.json` | `application.yml` or `application.properties` |
| `appsettings.SIT.json` | `application-sit.yml` |
| Typed `Config` object | `@ConfigurationProperties` class or record |
| Data annotation validation | Jakarta Bean Validation annotations plus `@Validated` |
| Singleton config registration | Configuration-properties bean, singleton by default |
| Environment name | Spring profile such as `dev`, `sit`, `uat`, `dr`, or `prod` |

Apply the same rules for startup validation, secret storage, environment isolation, override order, and fail-fast behavior. Prefer one immutable configuration object per prefix. Do not scatter `@Value` expressions or inject `Environment` throughout business code.

```java
@Validated
@ConfigurationProperties("catalog-client")
public record CatalogClientProperties(
    @NotBlank String baseUrl,
    @Min(1) @Max(300) int timeoutSeconds) {
}
```

Spring Boot supports structured externalized configuration and profile-specific files. Keep production secrets outside committed configuration files.

## Controllers, Requests, And Responses

| API concern | ASP.NET Core | Spring MVC |
| --- | --- | --- |
| Controller | `[ApiController]`, `[Route]` | `@RestController`, `@RequestMapping` |
| Query string | `[FromQuery]` | `@RequestParam` or `@ModelAttribute` query DTO |
| Path variable | `[FromRoute]` | `@PathVariable` |
| JSON body | `[FromBody]` | `@RequestBody` |
| Validation | automatic model validation | `@Valid` or `@Validated` |
| Multipart | `[FromForm]`, `IFormFile` | `@RequestPart`, `MultipartFile` |
| Status-aware response | `ActionResult<T>` | `ResponseEntity<T>` |
| Create response | `CreatedAtRoute` | `ResponseEntity.created(location)` |
| Error response | `ProblemDetails` | `ProblemDetail` |
| Binary response | `FileStreamResult` | `ResponseEntity<Resource>` or `StreamingResponseBody` |

Keep the same HTTP semantics, including `200 OK` as an allowed successful POST response when the contract requires it. Preserve paging limits, request-size limits, safe file names, upload validation, UTC timestamps, and RFC problem responses.

For JSON streaming, do not translate `IAsyncEnumerable<T>` mechanically:

- In Spring MVC, use bounded paging by default. Use `StreamingResponseBody`, `ResponseBodyEmitter`, or an explicitly supported streaming media type when streaming is intentional.
- In Spring WebFlux, use `Flux<Dto>` for multi-item reactive streams.
- Keep persistence resources open for the complete stream and propagate client disconnect or cancellation signals where the selected stack supports them.

## Services, Queries, And Transactions

### Query composition

| .NET / EF Core | Java / Spring Data JPA |
| --- | --- |
| `Expression<Func<TEntity, bool>>` predicate | `Specification<TEntity>`, Criteria API predicate, or a repository query method |
| `Expression<Func<TEntity, TResult>>` selector | Spring Data projection, JPQL constructor projection, or criteria selection |
| `ids.Contains(entity.Id)` | `findByIdIn(Collection<ID>)` or `WHERE entity.id IN :ids` |
| `AnyAsync()` | repository `exists...` query |
| `ToListAsync()` | repository `find...`, specification terminal method, or `getResultList()` |
| `IQueryable<T>` | JPA Criteria query, Spring Data fluent query, or repository abstraction |

Keep terminal operations in the layer that owns the query source. Controllers must not receive `IQueryable`, JPA `CriteriaQuery`, `Stream<Entity>`, or repositories. Apply filtering, ordering, projection, paging, and materialization inside the persistence-owning service or repository boundary.

For `WHERE IN`, validate and deduplicate IDs, handle empty input without invalid SQL, and batch large collections according to database and driver limits.

### Read-only queries

JPA has no exact equivalent to EF Core `AsNoTracking()`. Use the closest suitable technique:

- Prefer DTO or interface projections so entities are not materialized when they are not needed.
- Use `@Transactional(readOnly = true)` for read service operations.
- Avoid mutating managed entities in read paths.
- Use provider-specific read-only hints only when profiling shows a benefit and the behavior is tested.

Do not describe `@Transactional(readOnly = true)` as identical to `AsNoTracking()`; a JPA persistence context may still manage loaded entities.

### Transactions

In .NET, explicit EF Core transactions are appropriate for multi-step atomic operations. In Spring, place `@Transactional` on the public service method that owns the complete business operation.

```java
@Service
public class OrderService {
    @Transactional
    public OrderDto capturePayment(CapturePaymentRequest request) {
        // Update all required aggregates inside one service transaction.
        return result;
    }
}
```

Rules:

- Keep the transaction boundary at the service layer.
- Do not split one consistency boundary across multiple controller or client calls.
- Understand Spring proxy behavior: self-invocation does not start a new proxied transaction.
- Keep remote network calls outside database transactions where possible.
- Use an outbox or equivalent pattern when database state and message publication require reliable coordination.

## Persistence Mapping

This is the largest non-equivalence between the standards.

The .NET standard mandates EF Core Fluent API, `HasDefaultSchema(...)`, explicit `ToTable(..., schema)`, and annotation-free entities. Idiomatic JPA commonly uses mapping annotations such as `@Entity`, `@Table`, `@Id`, and relationship annotations.

Choose and document one Java policy:

1. **Idiomatic JPA policy:** permit JPA mapping annotations on persistence entities, require explicit `@Table(name = ..., schema = ...)`, and keep API validation annotations on DTOs rather than entities.
2. **Annotation-free persistence policy:** use `META-INF/orm.xml` for mappings and configure a default schema. This is the closer conceptual match to EF Fluent API but is less common in Spring applications.

Do not pretend that Hibernate annotations are equivalent to EF Fluent API. Whichever policy is selected, require explicit table/schema mapping, keys, lengths, precision, indexes, constraints, and relationships. Keep JPA entities out of API responses.

## Exceptions And Validation

Use `@RestControllerAdvice` with focused `@ExceptionHandler` methods and return Spring `ProblemDetail` responses. Do not expose stack traces, SQL, internal paths, or confidential values.

Use Jakarta Bean Validation on request DTOs:

```java
public record CreateProductRequest(
    @NotBlank @Size(max = 120) String name,
    @DecimalMin("0.01") BigDecimal unitPrice,
    @PositiveOrZero int stockQuantity) {
}
```

Keep database-dependent validation and business invariants in services. Treat unique constraints as the final concurrency-safe enforcement even when an early existence check improves the client message.

## Dependency Injection

Constructor injection remains mandatory in both stacks.

| .NET lifetime | Closest Spring concept |
| --- | --- |
| Singleton | singleton bean, Spring's default |
| Scoped per request | request-scoped bean or a scoped persistence-context proxy |
| Transient | prototype bean |

The mapping is not one-to-one. Spring services are usually singleton and stateless, while Spring manages request/transaction-bound resources through proxies. Do not store request data or mutable operation state in singleton beans. Avoid injecting a shorter-lived concrete bean into a singleton without a scoped proxy or provider.

Use explicit `@Configuration` and `@Bean` methods for infrastructure wiring. Use component scanning for cohesive application components, but avoid making package scanning so broad that registrations become accidental.

## Integration Clients

Create one typed wrapper per downstream API. Inject a configured `RestClient.Builder` for blocking Spring MVC applications or `WebClient.Builder` for reactive/non-blocking applications. Do not instantiate a new client per request.

Apply the same constraints as the .NET standard:

- Set connection, response, and overall call timeouts intentionally.
- Retry only safe operations and only transient failures.
- Use exponential backoff with jitter and a bounded attempt count.
- Add circuit breaking only where it improves failure isolation.
- Propagate correlation and trace context.
- Keep credentials and client certificates in secure configuration.
- Do not log tokens or sensitive payloads.
- Hide REST, GraphQL, and gRPC transport details behind client interfaces.

Java equivalents include Spring `RestClient` or `WebClient`, Spring GraphQL clients, generated gRPC stubs, and a resilience library such as Resilience4j where required.

## Logging

Use the SLF4J API with Logback as the usual Spring Boot implementation. Write to both console and files. Configure time- and size-based rolling with 10 MB as the default maximum file size, matching the .NET standard.

```xml
<appender name="FILE" class="ch.qos.logback.core.rolling.RollingFileAppender">
  <file>logs/service.log</file>
  <rollingPolicy class="ch.qos.logback.core.rolling.SizeAndTimeBasedRollingPolicy">
    <fileNamePattern>logs/service.%d{yyyy-MM-dd}.%i.log.gz</fileNamePattern>
    <maxFileSize>10MB</maxFileSize>
    <maxHistory>30</maxHistory>
  </rollingPolicy>
  <encoder>
    <pattern>%d %-5level [%X{correlationId}] %logger - %msg%n</pattern>
  </encoder>
</appender>
```

Use SLF4J placeholders instead of string concatenation:

```java
log.info("Created product {} for customer {}", productId, customerId);
```

Keep the same sensitivity, correlation, retention, and centralized aggregation rules.

## Application Bootstrap

`Program.cs` and the Spring Boot application class are both composition roots, but Spring commonly distributes infrastructure registration across focused `@Configuration` classes.

| .NET | Java |
| --- | --- |
| `Program.cs` | `@SpringBootApplication` main class |
| `IServiceCollection` extensions | focused `@Configuration` classes and `@Bean` methods |
| middleware pipeline | servlet filters, Spring MVC interceptors, exception advice, and framework configuration |
| startup configuration validation | configuration-properties validation during context startup |

Keep the main application class small. Do not place business logic, database seeding workflows, or environment-specific branching throughout bootstrap code.

## Async, Cancellation, And Performance

Do not mechanically translate `Task`, `async`/`await`, and `CancellationToken` to `CompletableFuture`.

- Traditional Spring MVC is request-thread based; use synchronous service signatures when work is blocking.
- Virtual threads may improve concurrency for blocking I/O but do not make blocking operations non-blocking.
- Spring WebFlux uses `Mono` and `Flux`; keep the entire path reactive and avoid blocking JPA calls on event-loop threads.
- Use timeouts, interruption-aware operations, Reactor cancellation, and request lifecycle hooks according to the selected execution model.
- Do not mix JPA's blocking APIs into a reactive pipeline without an explicitly isolated scheduler and a clear reason.

The database rules remain the same: project required columns, page or scroll large results, prevent N+1 queries, use explicit fetch plans, index common filters, and measure before optimizing.

## Formatting And Static Analysis

| .NET | Java |
| --- | --- |
| `.editorconfig` | `.editorconfig` plus formatter configuration |
| `dotnet format` | Spotless or a selected Java formatter |
| .NET analyzers / StyleCop | Checkstyle, PMD, SpotBugs, Error Prone, or equivalent selected tools |
| nullable reference types | nullness annotations and a configured nullness analyzer |
| warnings as errors | fail the Maven or Gradle build on selected analysis violations |

Select one formatter and one enforceable analysis baseline. Do not combine overlapping tools without clear ownership of each rule.

## Versioning And OpenAPI

Use the same API contract rules in both stacks: prefer URL path versioning such as `/api/v1/products`, apply it consistently, document deprecation and sunset dates, and keep old versions during the supported migration window.

For Java, springdoc-openapi is a common OpenAPI integration. Treat it as a dependency that requires normal version, vulnerability, and compatibility review. Restrict API documentation endpoints in production according to the same policy as the .NET service.

## Health Checks And Background Work

| .NET | Java |
| --- | --- |
| health-check endpoints | Spring Boot Actuator health endpoint/groups |
| liveness/readiness tags | Actuator liveness and readiness health groups |
| `IHealthCheck` | `HealthIndicator` |
| `BackgroundService` | `@Scheduled`, `TaskScheduler`, lifecycle-managed worker, or messaging listener |
| `CancellationToken` shutdown | application-context shutdown and interruption-aware worker logic |

Keep liveness independent of downstream systems. Put database and downstream dependency checks in readiness. Spring Boot exposes dedicated liveness and readiness groups when configured for probes.

Background work must have bounded concurrency, error handling, observability, idempotency where needed, and graceful shutdown. Do not start unmanaged threads from controllers or services.

## Testing And Coverage

| .NET | Java |
| --- | --- |
| xUnit v3 | JUnit Jupiter |
| Moq | Mockito |
| controller unit tests | `@WebMvcTest` with MockMvc/MockMvcTester, or plain controller tests |
| EF integration tests | repository tests against a real database or Testcontainers |
| Coverlet | JaCoCo |

Keep the same coverage requirements:

- Overall line coverage must be at least 80%.
- New or changed code coverage must be at least 90%.
- Unit tests must not call real databases or downstream APIs.
- Persistence translation and query behavior belong in integration tests.
- Prefer focused test slices over loading the full Spring context for every test.
- Use Testcontainers when database-specific behavior matters.

## Dependencies And Vulnerabilities

| .NET command or feature | Maven/Gradle equivalent |
| --- | --- |
| `dotnet list package --outdated` | Maven Versions Plugin or Gradle dependency-update tooling |
| `dotnet list package --deprecated` | no universal direct equivalent; review metadata and migration notices |
| `dotnet list package --vulnerable --include-transitive` | OWASP Dependency-Check, repository security scanning, or an approved software composition analysis tool |
| central package management | Maven dependency management/BOM or Gradle version catalog/platform |
| `packages.lock.json` | Maven reproducible dependency management or Gradle dependency locking and verification |

Apply the same severity policy: critical vulnerabilities block merge; high vulnerabilities block unless risk acceptance is documented; transitive dependencies must be included; major upgrades require focused regression testing.

For Maven, run at least:

```bash
./mvnw verify
./mvnw dependency:tree
./mvnw dependency:analyze
```

Add an approved vulnerability scanner to CI and schedule dependency freshness checks. Commit Maven Wrapper or Gradle Wrapper files so builds use a controlled tool version.

## Key Differences To Preserve

1. **Persistence mapping:** EF Fluent API has no common direct Spring Data JPA equivalent. Select annotations or `orm.xml` explicitly rather than claiming parity.
2. **Read-only behavior:** `AsNoTracking()` and `@Transactional(readOnly = true)` are not equivalent.
3. **Async behavior:** `Task` and `CancellationToken` do not map directly to `CompletableFuture`, virtual threads, or Reactor.
4. **DI lifetimes:** .NET request-scoped services and Spring singleton services use different default lifetime models.
5. **Transactions:** explicit EF transactions and Spring proxy-based `@Transactional` have different failure modes and invocation rules.
6. **Query APIs:** LINQ expression trees map conceptually to specifications, criteria, projections, and repository queries, not Java Streams over loaded entities.

## Java Review Checklist

- [ ] Controllers delegate to services and do not access repositories directly.
- [ ] API records/DTOs are separate from JPA entities.
- [ ] Configuration uses validated `@ConfigurationProperties` objects.
- [ ] Persistence mappings include explicit table and schema names under the selected Java mapping policy.
- [ ] Read paths use projections or an intentional read-only strategy.
- [ ] Repository/specification queries are materialized inside the owning layer.
- [ ] Large `IN` collections are validated, deduplicated, and batched when necessary.
- [ ] Service methods own `@Transactional` boundaries for multi-step consistency.
- [ ] Errors use consistent `ProblemDetail` responses from centralized advice.
- [ ] Request DTOs use Jakarta Bean Validation.
- [ ] Outbound clients are reused, typed, time-bounded, resilient, and securely configured.
- [ ] Logs go to console and daily/size-rolled files with a 10 MB default limit.
- [ ] The selected MVC, virtual-thread, or reactive execution model is used consistently.
- [ ] Liveness and readiness are separated.
- [ ] JUnit Jupiter tests and JaCoCo meet the 80% overall and 90% new-code targets.
- [ ] Direct and transitive dependencies pass vulnerability checks.
- [ ] `./mvnw verify` or the Gradle equivalent passes with no unapproved warnings or failures.

## Primary References

- [Spring Boot externalized configuration](https://docs.spring.io/spring-boot/reference/features/external-config.html)
- [Spring MVC `ResponseEntity`](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-methods/responseentity.html)
- [Spring MVC error responses and `ProblemDetail`](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-ann-rest-exceptions.html)
- [Spring MVC multipart handling](https://docs.spring.io/spring-framework/reference/web/webmvc/mvc-controller/ann-methods/multipart-forms.html)
- [Spring Framework REST clients](https://docs.spring.io/spring-framework/reference/integration/rest-clients.html)
- [Spring Framework `WebClient`](https://docs.spring.io/spring-framework/reference/web/webflux-webclient.html)
- [Spring Data JPA specifications](https://docs.spring.io/spring-data/jpa/reference/jpa/specifications.html)
- [Spring Data JPA projections](https://docs.spring.io/spring-data/jpa/reference/repositories/projections.html)
- [Spring Data JPA transactionality](https://docs.spring.io/spring-data/jpa/reference/jpa/transactions.html)
- [Spring Boot logging](https://docs.spring.io/spring-boot/reference/features/logging.html)
- [Spring Boot Actuator endpoints and probes](https://docs.spring.io/spring-boot/reference/actuator/endpoints.html)
- [Spring Boot testing](https://docs.spring.io/spring-boot/reference/testing/)
- [Spring Boot Testcontainers support](https://docs.spring.io/spring-boot/reference/testing/testcontainers.html)
- [Apache Maven Dependency Plugin](https://maven.apache.org/plugins/maven-dependency-plugin/)
- [OWASP Dependency-Check Maven plugin](https://jeremylong.github.io/DependencyCheck/dependency-check-maven/)
