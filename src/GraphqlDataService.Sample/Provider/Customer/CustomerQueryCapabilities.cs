using System.Globalization;
using GraphqlDataService.Sample.GraphQL.Export;
using CustomerEntity = GraphqlDataService.Sample.Provider.Customer.Data.Customer;

namespace GraphqlDataService.Sample.Provider.Customer;

public sealed class CustomerQueryCapabilities : EntityExportQueryCapabilities<CustomerEntity>
{
    private static readonly IReadOnlySet<ExportFilterOperator> Equality =
        Set(ExportFilterOperator.Equal);

    private static readonly IReadOnlyList<IEntityExportField<CustomerEntity>> CustomerFields =
    [
        new EntityExportField<CustomerEntity, Guid>(
            "id",
            customer => customer.Id,
            Equality,
            (query, filter) => Guid.TryParse(filter.Value, out var id)
                ? query.Where(customer => customer.Id == id)
                : throw InvalidFilter("id"),
            customer => customer.Id.ToString("D")),
        new EntityExportField<CustomerEntity, string>(
            "name",
            customer => customer.Name,
            Set(ExportFilterOperator.Equal, ExportFilterOperator.Contains),
            (query, filter) => filter.Operator switch
            {
                ExportFilterOperator.Equal =>
                    query.Where(customer => customer.Name == filter.Value),
                ExportFilterOperator.Contains =>
                    query.Where(customer => customer.Name.Contains(filter.Value)),
                _ => throw InvalidFilter("name")
            },
            customer => customer.Name ?? string.Empty),
        new EntityExportField<CustomerEntity, string>(
            "email",
            customer => customer.Email,
            Set(ExportFilterOperator.Equal, ExportFilterOperator.Contains),
            (query, filter) => filter.Operator switch
            {
                ExportFilterOperator.Equal =>
                    query.Where(customer => customer.Email == filter.Value),
                ExportFilterOperator.Contains =>
                    query.Where(customer => customer.Email.Contains(filter.Value)),
                _ => throw InvalidFilter("email")
            },
            customer => customer.Email ?? string.Empty),
        new EntityExportField<CustomerEntity, DateOnly?>(
            "birthDate",
            customer => customer.BirthDate,
            Set(
                ExportFilterOperator.Equal,
                ExportFilterOperator.GreaterThanOrEqual,
                ExportFilterOperator.LessThanOrEqual),
            ApplyBirthDateFilter,
            customer => customer.BirthDate?.ToString(
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture) ?? string.Empty)
    ];

    protected override IReadOnlyList<IEntityExportField<CustomerEntity>> Fields => CustomerFields;

    private static IQueryable<CustomerEntity> ApplyBirthDateFilter(
        IQueryable<CustomerEntity> query,
        ExportFilterInput filter)
    {
        if (!DateOnly.TryParse(
                filter.Value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            throw InvalidFilter("birthDate");
        }

        return filter.Operator switch
        {
            ExportFilterOperator.Equal =>
                query.Where(customer => customer.BirthDate == date),
            ExportFilterOperator.GreaterThanOrEqual =>
                query.Where(customer => customer.BirthDate >= date),
            ExportFilterOperator.LessThanOrEqual =>
                query.Where(customer => customer.BirthDate <= date),
            _ => throw InvalidFilter("birthDate")
        };
    }

    private static IReadOnlySet<ExportFilterOperator> Set(
        params ExportFilterOperator[] operators) => operators.ToHashSet();

    private static ExportGenerationException InvalidFilter(string field) =>
        new("EXPORT_FILTER_INVALID", $"Unsupported filter for field '{field}'.");
}
