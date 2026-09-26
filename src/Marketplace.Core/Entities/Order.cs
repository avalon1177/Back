using Marketplace.Core.Common;
using Marketplace.Core.Enums;

namespace Marketplace.Core.Entities;

public class Order : Entity<Guid>
{
    public Guid BuyerUserId { get; set; }
    public AppUser BuyerUser { get; set; } = default!;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public Guid? ShippingAddressId { get; set; }
    public Address? ShippingAddress { get; set; }
    public Guid? ShippingMethodId { get; set; }
    public ShippingMethod? ShippingMethod { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string DeliveryAddress { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public string? TrackingNumber { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<OrderCoupon> Coupons { get; set; } = new List<OrderCoupon>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
