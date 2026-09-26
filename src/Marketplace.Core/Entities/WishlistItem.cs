using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class WishlistItem : Entity<Guid>
{
    public Guid WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = default!;
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
