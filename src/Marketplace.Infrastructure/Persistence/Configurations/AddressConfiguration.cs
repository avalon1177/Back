using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> e)
    {
        e.Property(x => x.Name).HasMaxLength(80);
        e.Property(x => x.Phone).HasMaxLength(30);
        e.Property(x => x.Region).HasMaxLength(120);
        e.Property(x => x.City).HasMaxLength(120);
        e.Property(x => x.Line1).HasMaxLength(200);
        e.Property(x => x.Line2).HasMaxLength(200);
        e.Property(x => x.PostalCode).HasMaxLength(20);
        e.HasOne(x => x.User).WithMany(x => x.Addresses).HasForeignKey(x => x.UserId);
    }
}
