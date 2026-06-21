using GraphqlDataService.Generator;

namespace GraphqlDataService.Sample.Tests.Generator;

public sealed class SourceGeneratorTests
{
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
}
