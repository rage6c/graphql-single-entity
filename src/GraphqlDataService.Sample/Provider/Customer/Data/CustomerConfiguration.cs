using GraphqlDataService.Sample.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GraphqlDataService.Sample.Provider.Customer.Data;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> entity)
    {
        entity.ToTable("customers", DatabaseSchemas.Application);
        entity.HasKey(customer => customer.Id);
        entity.Property(customer => customer.Name).HasMaxLength(200).IsRequired();
        entity.Property(customer => customer.Email).HasMaxLength(320).IsRequired();
        entity.HasIndex(customer => customer.Email).IsUnique();
    }
}
