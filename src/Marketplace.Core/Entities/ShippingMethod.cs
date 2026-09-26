using Marketplace.Core.Common;
using Marketplace.Core.Enums;

namespace Marketplace.Core.Entities;

public class ShippingMethod : Entity<Guid>
{
    public ShippingMethodType Name { get; set; }
    public decimal Price { get; set; }
    public int EstimatedDays { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
