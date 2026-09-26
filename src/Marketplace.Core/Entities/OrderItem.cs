using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class OrderItem : Entity<Guid>
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;

    public string ProductTitleSnapshot { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
    public DateTime? ReturnApprovedAtUtc { get; set; }
}
