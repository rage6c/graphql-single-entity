# Formatting And Style

- Nullable reference types, implicit usings, analyzers, and warnings-as-errors are enabled centrally.
- Use one public provider responsibility per file, except the paired input records/types in `{Entity}Inputs.cs`.
- Use file-scoped namespaces and sealed concrete provider classes.
- Apply `[ExtendObjectType]` to concrete query, mutation, and subscription classes; the generic bases are plain helper classes.
- Apply `[GraphQLName]` where the public field name is not the desired method-derived name.
- Query middleware order is paging, projection, filtering, sorting.
- Use `[GraphQLIgnore]` on concrete entity audit/internal properties.
- Return existing tasks directly from thin wrappers instead of unnecessary `async`/`await`.
- Keep GraphQL/source field names camelCase and CLR types PascalCase.

Generated source must follow the same rules. Scriban templates are source artifacts and must be reviewed alongside their generated output. Run `dotnet build` with zero warnings after template or infrastructure changes.
