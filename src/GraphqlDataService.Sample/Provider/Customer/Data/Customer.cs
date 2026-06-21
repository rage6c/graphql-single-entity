using GraphqlDataService.Sample.Data.Entities;

namespace GraphqlDataService.Sample.Provider.Customer.Data;

public sealed class Customer : EntityBase
{
    public Guid Id { get; set; }

    public required string Name { get; set; }

    public required string Email { get; set; }

    public DateOnly? BirthDate { get; set; }

    [GraphQLIgnore]
    public DateTimeOffset CreatedAt { get; set; }

    [GraphQLIgnore]
    public DateTimeOffset UpdatedAt { get; set; }
}
