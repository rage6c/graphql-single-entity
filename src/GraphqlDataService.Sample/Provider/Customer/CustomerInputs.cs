namespace GraphqlDataService.Sample.Provider.Customer;

public sealed record CustomerCreateInput(string Name, string Email, DateOnly? BirthDate);

public sealed record CustomerUpdateInput(
    Guid Id,
    Optional<string> Name,
    Optional<string> Email,
    Optional<DateOnly?> BirthDate);

public sealed class CustomerCreateInputType : InputObjectType<CustomerCreateInput>;

public sealed class CustomerUpdateInputType : InputObjectType<CustomerUpdateInput>;
