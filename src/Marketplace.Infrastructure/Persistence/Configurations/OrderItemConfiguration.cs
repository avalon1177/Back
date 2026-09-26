using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> e)
    {
        e.Property(x => x.UnitPrice).HasPrecision(18, 2);
        e.Property(x => x.LineTotal).HasPrecision(18, 2);
        e.Property(x => x.ProductTitleSnapshot).HasMaxLength(160);
        e.HasOne(x => x.Order).WithMany(x => x.Items).HasForeignKey(x => x.OrderId);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
        e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId);
    }
}
