# Review Checklist And Examples

## Provider

- [ ] Folder is `Provider/{Entity}` with entity/configuration under `Data`.
- [ ] Query, mutation, and subscription carry the correct `[ExtendObjectType]` attribute.
- [ ] Query derives from `CrudQuery<TEntity>` and uses middleware in the required order.
- [ ] Mutation derives from `CrudMutation<TEntity,TKey,TCreateInput,TUpdateInput,TMapper>`.
- [ ] Mapper implements `ICrudMapper<TEntity,TKey,TCreateInput,TUpdateInput>`.
- [ ] Concrete entity declares its own key/audit properties; `EntityBase` stays empty.
- [ ] Delete is hard delete everywhere.

## Discovery And Data

- [ ] Provider namespace is beneath both configured namespace prefixes when GraphQL and EF discovery are required.
- [ ] No manual Customer registration is added to `Program.cs`.
- [ ] EF mapping uses the real table/schema/column names and primary key.
- [ ] `GridSchema` remains infrastructure-only.

## Query And Export

- [ ] Field definitions are declared once and reused for GraphQL filtering/sorting and export validation/execution.
- [ ] Export columns are non-empty, unique, ordered, and validated against the active grid.
- [ ] Export projection remains server-side and is bounded by rows and bytes.
- [ ] CSV/Excel values receive spreadsheet-injection protection.
- [ ] Subscription export IDs use GraphQL UUID values without embedding extra quote characters.

## Current Gaps To Flag

- [ ] Auth and export ownership are added before production.
- [ ] Configured depth and export concurrency are actually enforced before claiming them.
- [ ] Database uniqueness/concurrency exceptions receive stable error mappings if clients depend on them.
- [ ] Fresh deployments provision required `gridSchema` rows.
- [ ] Source generator output is reviewed for indexes and stale database columns.

## Verification

```bash
dotnet build GraphqlDataService.Sample.slnx --no-restore
dotnet test GraphqlDataService.Sample.slnx --no-build --no-restore
dotnet list package --vulnerable --include-transitive
```
