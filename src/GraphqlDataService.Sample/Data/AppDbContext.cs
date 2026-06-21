using GraphqlDataService.Sample.Configuration;
using GraphqlDataService.Sample.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GraphqlDataService.Sample.Data;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IOptions<DatabaseConfig> databaseOptions) : DbContext(options)
{
    private readonly string _providerNamespacePrefix =
        databaseOptions.Value.ProviderNamespacePrefix.Trim().TrimEnd('.');

    public DbSet<GridSchema> GridSchemas => Set<GridSchema>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DatabaseSchemas.Application);

        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly,
            type => type.Namespace is not null &&
                (type.Namespace == _providerNamespacePrefix ||
                 type.Namespace.StartsWith(
                     _providerNamespacePrefix + ".",
                     StringComparison.Ordinal)));

        modelBuilder.Entity<GridSchema>(entity =>
        {
            entity.ToTable("gridSchema", DatabaseSchemas.Application);
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Definition).HasColumnType("jsonb");
            entity.HasIndex(x => new { x.EntityName, x.ViewName }).IsUnique();
        });
    }
}
