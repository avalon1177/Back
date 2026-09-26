using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> e)
    {
        e.Property(x => x.Amount).HasPrecision(18, 2);
        e.Property(x => x.ExternalReference).HasMaxLength(128);
        e.HasOne(x => x.Order).WithMany(x => x.Payments).HasForeignKey(x => x.OrderId);
    }
}
