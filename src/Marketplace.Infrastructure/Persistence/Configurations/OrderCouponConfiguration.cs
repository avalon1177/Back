using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class OrderCouponConfiguration : IEntityTypeConfiguration<OrderCoupon>
{
    public void Configure(EntityTypeBuilder<OrderCoupon> e)
    {
        e.HasIndex(x => new { x.OrderId, x.CouponId }).IsUnique();
        e.HasOne(x => x.Order).WithMany(x => x.Coupons).HasForeignKey(x => x.OrderId);
        e.HasOne(x => x.Coupon).WithMany(x => x.Orders).HasForeignKey(x => x.CouponId);
    }
}
