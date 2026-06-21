using GraphqlDataService.Generator;

namespace GraphqlDataService.Sample.Tests.Generator;

public sealed class SourceGeneratorTests
{
    [Fact]
    public void SchemaDbContext_ConfiguresSelectedProvider()
    {
        using var postgres = new SchemaDbContext(
            DatabaseProvider.PostgreSql,
            "Host=localhost;Database=test;Username=test;Password=test");
        using var sqlServer = new SchemaDbContext(
            DatabaseProvider.SqlServer,
            "Server=localhost;Database=test;User Id=test;Password=test;TrustServerCertificate=true");

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", postgres.Database.ProviderName);
        Assert.Equal("Microsoft.EntityFrameworkCore.SqlServer", sqlServer.Database.ProviderName);
    }

    [Fact]
    public void GeneratorOptions_ExposeDefaultsAndConfiguredValues()
    {
        var defaults = new GeneratorOptions();
        var configured = new GeneratorOptions
        {
            Provider = "Mssql",
            ConnectionString = "connection",
            Namespace = "Example",
            OutputPath = "output",
            Schema = "dbo",
            Tables = ["orders"]
        };

        Assert.Equal("PostgreSql", defaults.Provider);
        Assert.Empty(defaults.Tables);
        Assert.Equal("Mssql", configured.Provider);
        Assert.Equal("connection", configured.ConnectionString);
        Assert.Equal("Example", configured.Namespace);
        Assert.Equal("output", configured.OutputPath);
        Assert.Equal("dbo", configured.Schema);
        Assert.Equal(["orders"], configured.Tables);
    }

    [Fact]
    public async Task MaterializeAsync_FiltersAndGroupsSchemaRows()
    {
        var table = new System.Data.DataTable();
        table.Columns.Add("schema", typeof(string));
        table.Columns.Add("table", typeof(string));
        table.Columns.Add("column", typeof(string));
        table.Columns.Add("type", typeof(string));
        table.Columns.Add("nullable", typeof(bool));
        table.Columns.Add("primary", typeof(bool));
        table.Columns.Add("identity", typeof(bool));
        table.Columns.Add("length", typeof(int));
        table.Columns.Add("ordinal", typeof(int));
        table.Rows.Add("app", "customers", "name", "varchar", false, false, false, 200, 2);
        table.Rows.Add("app", "customers", "id", "uuid", false, true, false, DBNull.Value, 1);
        table.Rows.Add("app", "orders", "id", "int8", false, true, true, DBNull.Value, 1);
        await using var reader = table.CreateDataReader();

        var result = await DatabaseSchemaReader.MaterializeAsync(
            reader,
            ["CUSTOMERS"],
            TestContext.Current.CancellationToken);

        var customers = Assert.Single(result);
        Assert.Equal("customers", customers.Name);
        Assert.Equal(["id", "name"], customers.Columns.Select(column => column.Name));
        Assert.Null(customers.Columns[0].MaxLength);
        Assert.Equal(200, customers.Columns[1].MaxLength);
    }

    [Fact]
    public void Query_UsesProviderSpecificMetadataSql()
    {
        var postgres = new DatabaseSchemaReader(DatabaseProvider.PostgreSql).Query;
        var sqlServer = new DatabaseSchemaReader(DatabaseProvider.SqlServer).Query;

        Assert.Contains("udt_name", postgres, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COLUMNPROPERTY", sqlServer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateAsync_CreatesCustomerProviderShape()
    {
        var table = new DatabaseTable(
            "app",
            "customers",
            [
                new("app", "customers", "id", "uuid", false, true, false, null, 1),
                new("app", "customers", "name", "varchar", false, false, false, 200, 2),
                new("app", "customers", "birth_date", "date", true, false, false, null, 3),
                new("app", "customers", "created_at", "timestamptz", false, false, false, null, 4),
                new("app", "customers", "updated_at", "timestamptz", false, false, false, null, 5)
            ]);
        var model = new EntityModelFactory(DatabaseProvider.PostgreSql, "Example.Service")
            .Create(table);
        var output = Path.Combine(Path.GetTempPath(), $"graphql-generator-{Guid.NewGuid():N}");

        try
        {
            var templates = Path.Combine(AppContext.BaseDirectory, "GeneratorTemplates");
            await new SourceGenerator(templates).GenerateAsync(
                model,
                output,
                TestContext.Current.CancellationToken);

            var provider = Path.Combine(output, "Customer");
            Assert.True(File.Exists(Path.Combine(provider, "CustomerQuery.cs")));
            Assert.True(File.Exists(Path.Combine(provider, "CustomerMutation.cs")));
            Assert.True(File.Exists(Path.Combine(provider, "CustomerQueryCapabilities.cs")));
            Assert.True(File.Exists(Path.Combine(provider, "Data", "Customer.cs")));

            var mutation = await File.ReadAllTextAsync(
                Path.Combine(provider, "CustomerMutation.cs"),
                TestContext.Current.CancellationToken);
            Assert.Contains("CrudMutation<CustomerEntity, Guid,", mutation);
            Assert.Contains("deleteCustomer", mutation);

            var configuration = await File.ReadAllTextAsync(
                Path.Combine(provider, "Data", "CustomerConfiguration.cs"),
                TestContext.Current.CancellationToken);
            Assert.Contains("entity.ToTable(\"customers\", \"app\")", configuration);
            Assert.Contains(".HasColumnName(\"birth_date\")", configuration);

            foreach (var file in Directory.EnumerateFiles(provider, "*.cs", SearchOption.AllDirectories))
            {
                var source = await File.ReadAllTextAsync(
                    file,
                    TestContext.Current.CancellationToken);
                Assert.DoesNotContain("{{", source);
                Assert.DoesNotContain("}}", source);
            }
        }
        finally
        {
            if (Directory.Exists(output))
            {
                Directory.Delete(output, recursive: true);
            }
        }
    }

    [Fact]
    public void Create_SqlServerIdentityLongKey_UsesLongKeyWithoutGeneratingIt()
    {
        var table = new DatabaseTable(
            "dbo",
            "orders",
            [
                new("dbo", "orders", "id", "bigint", false, true, true, null, 1),
                new("dbo", "orders", "total", "decimal", false, false, false, null, 2)
            ]);

        var model = new EntityModelFactory(DatabaseProvider.SqlServer, "Example.Service")
            .Create(table);

        Assert.Equal("long", model.KeyType);
        Assert.False(model.GenerateGuidKey);
        Assert.Equal("Order", model.EntityName);
    }

    [Theory]
    [InlineData(DatabaseProvider.PostgreSql, "int2", "short")]
    [InlineData(DatabaseProvider.PostgreSql, "bool", "bool")]
    [InlineData(DatabaseProvider.PostgreSql, "numeric", "decimal")]
    [InlineData(DatabaseProvider.PostgreSql, "float4", "float")]
    [InlineData(DatabaseProvider.PostgreSql, "float8", "double")]
    [InlineData(DatabaseProvider.PostgreSql, "time", "TimeOnly")]
    [InlineData(DatabaseProvider.PostgreSql, "timestamp", "DateTime")]
    [InlineData(DatabaseProvider.PostgreSql, "timestamptz", "DateTimeOffset")]
    [InlineData(DatabaseProvider.PostgreSql, "jsonb", "string")]
    [InlineData(DatabaseProvider.SqlServer, "tinyint", "byte")]
    [InlineData(DatabaseProvider.SqlServer, "bit", "bool")]
    [InlineData(DatabaseProvider.SqlServer, "money", "decimal")]
    [InlineData(DatabaseProvider.SqlServer, "real", "float")]
    [InlineData(DatabaseProvider.SqlServer, "float", "double")]
    [InlineData(DatabaseProvider.SqlServer, "datetime2", "DateTime")]
    [InlineData(DatabaseProvider.SqlServer, "datetimeoffset", "DateTimeOffset")]
    [InlineData(DatabaseProvider.SqlServer, "nvarchar", "string")]
    public void Create_MapsSupportedDatabaseTypes(
        DatabaseProvider provider,
        string storeType,
        string expectedType)
    {
        var table = new DatabaseTable(
            "app",
            "items",
            [
                new("app", "items", "id", provider == DatabaseProvider.PostgreSql ? "uuid" : "uniqueidentifier", false, true, false, null, 1),
                new("app", "items", "value", storeType, true, false, false, null, 2)
            ]);

        var model = new EntityModelFactory(provider, "Example").Create(table);

        Assert.Equal(expectedType + "?", model.Columns[1].ClrType);
    }

    [Theory]
    [InlineData("no-key")]
    [InlineData("composite")]
    [InlineData("manual-int")]
    [InlineData("unsupported")]
    public void Create_InvalidSchema_Throws(string scenario)
    {
        DatabaseColumn[] columns = scenario switch
        {
            "no-key" => new[] { new DatabaseColumn("app", "items", "name", "text", false, false, false, null, 1) },
            "composite" =>
            [
                new("app", "items", "id", "uuid", false, true, false, null, 1),
                new("app", "items", "other_id", "uuid", false, true, false, null, 2)
            ],
            "manual-int" => [new("app", "items", "id", "int4", false, true, false, null, 1)],
            _ =>
            [
                new("app", "items", "id", "uuid", false, true, false, null, 1),
                new("app", "items", "shape", "geometry", false, false, false, null, 2)
            ]
        };

        Assert.Throws<InvalidOperationException>(() =>
            new EntityModelFactory(DatabaseProvider.PostgreSql, "Example")
                .Create(new DatabaseTable("app", "items", columns)));
    }
}
