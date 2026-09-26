using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class OrderCoupon : Entity<Guid>
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;
    public Guid CouponId { get; set; }
    public Coupon Coupon { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
