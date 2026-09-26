using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class CartItem : Entity<Guid>
{
    public Guid BuyerUserId { get; set; }
    public AppUser BuyerUser { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public int Quantity { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
