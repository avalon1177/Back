using Marketplace.Core.Common;
using Marketplace.Core.Enums;

namespace Marketplace.Core.Entities;

public class Coupon : Entity<Guid>
{
    public string Code { get; set; } = string.Empty;
    public decimal Discount { get; set; }
    public DiscountType DiscountType { get; set; }
    public int? UsageLimit { get; set; }
    public int UsedCount { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<OrderCoupon> Orders { get; set; } = new List<OrderCoupon>();
}
