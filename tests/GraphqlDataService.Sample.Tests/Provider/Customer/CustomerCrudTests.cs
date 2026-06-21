using GraphqlDataService.Sample.Provider.Customer;
using HotChocolate;
using Microsoft.EntityFrameworkCore;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Tests.Provider.Customer;

public sealed class CustomerCrudTests
{
    [Fact]
    public async Task CreateUpdateDelete_PerformsHardDelete()
    {
        await using var db = TestDb.Create();
        var mutation = new CustomerMutation(new CustomerMapper());

        var created = await mutation.CreateCustomerAsync(
            new CustomerCreateInput(" Customer ", " customer@example.test ", null),
            db,
            TestContext.Current.CancellationToken);
        var updated = await mutation.UpdateCustomerAsync(
            new CustomerUpdateInput(
                created.Id,
                new Optional<string>("Updated"),
                default,
                new Optional<DateOnly?>(new DateOnly(2000, 1, 2))),
            db,
            TestContext.Current.CancellationToken);
        var deleted = await mutation.DeleteCustomerAsync(
            created.Id,
            db,
            TestContext.Current.CancellationToken);

        Assert.Equal("Updated", updated.Name);
        Assert.Equal(new DateOnly(2000, 1, 2), updated.BirthDate);
        Assert.Equal(created.Id, deleted.Id);
        Assert.False(await db.Set<CustomerEntity>().AnyAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Update_WhenMissing_ReturnsNotFound()
    {
        await using var db = TestDb.Create();
        var mutation = new CustomerMutation(new CustomerMapper());

        var exception = await Assert.ThrowsAsync<GraphQLException>(() =>
            mutation.UpdateCustomerAsync(
                new CustomerUpdateInput(Guid.NewGuid(), default, default, default),
                db,
                TestContext.Current.CancellationToken));

        Assert.Equal("NOT_FOUND", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public async Task Delete_WhenMissing_ReturnsNotFound()
    {
        await using var db = TestDb.Create();
        var mutation = new CustomerMutation(new CustomerMapper());

        var exception = await Assert.ThrowsAsync<GraphQLException>(() =>
            mutation.DeleteCustomerAsync(
                Guid.NewGuid(),
                db,
                TestContext.Current.CancellationToken));

        Assert.Equal("NOT_FOUND", Assert.Single(exception.Errors).Code);
    }

    [Fact]
    public async Task Query_ReturnsNoTrackingQueryable()
    {
        await using var db = TestDb.Create();
        db.Add(new CustomerEntity
        {
            Id = Guid.NewGuid(),
            Name = "Customer",
            Email = "customer@example.test"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.ChangeTracker.Clear();

        var rows = await new CustomerQuery().GetCustomers(db).ToListAsync(
            TestContext.Current.CancellationToken);

        Assert.Single(rows);
        Assert.Empty(db.ChangeTracker.Entries());
    }
}
