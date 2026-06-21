using GraphqlDataService.Sample.Data;
using GraphqlDataService.Sample.Data.Entities;
using GraphqlDataService.Sample.GraphQL.Export;
using Microsoft.EntityFrameworkCore;

namespace GraphqlDataService.Sample.GraphQL.Infrastructure;

public abstract class CrudQuery<TEntity> where TEntity : EntityBase
{
    protected static IQueryable<TEntity> GetEntities(AppDbContext db) =>
        db.Set<TEntity>()
            .AsNoTracking();

    protected static Task<ExportJob> EnqueueExportAsync(
        ExportFileFormat format,
        IReadOnlyList<string> columns,
        IReadOnlyList<ExportFilterInput>? filters,
        IReadOnlyList<ExportOrderInput>? order,
        IEntityExportJobService<TEntity> jobs,
        CancellationToken cancellationToken) =>
        jobs.EnqueueAsync(format, columns, filters, order, cancellationToken);

    protected static Task<ExportJob> EnqueueGridViewExportAsync(
        string gridViewName,
        IEntityExportJobService<TEntity> jobs,
        CancellationToken cancellationToken) =>
        jobs.EnqueueByGridViewAsync(gridViewName, cancellationToken);
}
