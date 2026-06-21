# Testing With xUnit

The solution uses xUnit v3 in `tests/GraphqlDataService.Sample.Tests`.

Current coverage includes:

- `SchemaTests` building the Hot Chocolate schema and checking Customer operations;
- mapper create/update/validation behavior;
- export capability validation and exporter inheritance;
- YAML job-store behavior;
- EF model configuration discovery;
- Scriban provider rendering from representative PostgreSQL metadata;
- SQL Server identity-`long` key model generation.

The source-generator test copies all `.scriban` templates to test output, renders the full Customer provider shape, checks expected files and mappings, and rejects unrendered template tokens.

Run:

```bash
dotnet test GraphqlDataService.Sample.slnx --no-restore
./scripts/coverage.sh
```

Production line coverage must remain at or above 90%. The coverage script excludes the test assembly and composition-root top-level programs, includes both production assemblies, writes Cobertura and text reports under `TestResults`, and fails below the threshold.

Production-oriented additions still needed:

- Testcontainers integration tests for PostgreSQL and SQL Server metadata readers;
- HTTP GraphQL CRUD/paging/filtering tests;
- export worker and REST download lifecycle tests;
- hard-delete verification against a real database;
- multi-worker claim/lease tests;
- authentication/authorization tests after security is implemented.

Do not write tests for removed `CustomerGridDefinition` code; grid definitions are database-backed.
