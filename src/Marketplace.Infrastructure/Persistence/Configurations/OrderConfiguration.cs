using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.Property(x => x.Total).HasPrecision(18, 2);
        e.Property(x => x.BuyerName).HasMaxLength(80);
        e.Property(x => x.Phone).HasMaxLength(30);
        e.Property(x => x.City).HasMaxLength(80);
        e.Property(x => x.DeliveryAddress).HasMaxLength(200);
        e.Property(x => x.Comment).HasMaxLength(500);
        e.HasOne(x => x.BuyerUser).WithMany().HasForeignKey(x => x.BuyerUserId);
        e.HasOne(x => x.ShippingAddress).WithMany().HasForeignKey(x => x.ShippingAddressId).OnDelete(DeleteBehavior.SetNull);
        e.HasOne(x => x.ShippingMethod).WithMany().HasForeignKey(x => x.ShippingMethodId).OnDelete(DeleteBehavior.SetNull);
    }
}
