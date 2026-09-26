using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Chat : Entity<Guid>
{
    public Guid SellerId { get; set; }
    public AppUser Seller { get; set; } = default!;
    public Guid BuyerId { get; set; }
    public AppUser Buyer { get; set; } = default!;
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
