using Microsoft.EntityFrameworkCore;

namespace GraphqlDataService.Generator;

public sealed class SchemaDbContext : DbContext
{
    private readonly DatabaseProvider provider;
    private readonly string connectionString;

    public SchemaDbContext(DatabaseProvider provider, string connectionString)
    {
        this.provider = provider;
        this.connectionString = connectionString;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (provider == DatabaseProvider.PostgreSql)
        {
            optionsBuilder.UseNpgsql(connectionString);
        }
        else
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
    }
}
