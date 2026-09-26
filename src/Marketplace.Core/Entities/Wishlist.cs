using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Wishlist : Entity<Guid>
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
}
