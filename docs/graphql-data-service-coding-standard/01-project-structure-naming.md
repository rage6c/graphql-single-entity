# Project Structure And Naming

```text
src/
├── GraphqlDataService.Sample/
│   ├── Configuration/
│   ├── Data/
│   │   ├── AppDbContext.cs
│   │   └── Entities/
│   ├── GraphQL/
│   │   ├── Export/
│   │   ├── Grid/
│   │   └── Infrastructure/
│   └── Provider/
│       └── Customer/
│           ├── Data/
│           │   ├── Customer.cs
│           │   └── CustomerConfiguration.cs
│           ├── CustomerQuery.cs
│           ├── CustomerMutation.cs
│           ├── CustomerSubscription.cs
│           ├── CustomerInputs.cs
│           ├── CustomerMapper.cs
│           ├── CustomerFilterType.cs
│           ├── CustomerSortType.cs
│           ├── CustomerQueryCapabilities.cs
│           └── CustomerExportGenerator.cs
└── GraphqlDataService.Generator/
    └── Templates/
```

Rules:

- Create one singular PascalCase provider folder per exposed table.
- Keep entity-specific schema, mapping, inputs, and field metadata in that provider.
- Keep reusable CRUD, grid, and export machinery under `GraphQL`.
- Do not create root `Query.cs` or `Mutation.cs`; named Hot Chocolate roots are configured centrally.
- Do not expose `GridSchema` through provider CRUD.
- Use namespaces matching folders: `{Root}.Provider.{Entity}` and `{Root}.Provider.{Entity}.Data`.

| Item | Pattern | Customer example |
| --- | --- | --- |
| List field | plural camelCase | `customers` |
| CRUD fields | verb + singular entity | `createCustomer` |
| Export fields | `download{Plural}` and `download{Plural}ByGridView` | `downloadCustomers` |
| Status subscription | `download{Plural}Status` | `downloadCustomersStatus` |
| Filter/sort types | `{Entity}FilterType`, `{Entity}SortType` | `CustomerFilterType` |
| EF configuration | `{Entity}Configuration` | `CustomerConfiguration` |
