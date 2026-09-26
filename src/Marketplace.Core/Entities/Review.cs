using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Review : Entity<Guid>
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;
    public Guid BuyerUserId { get; set; }
    public AppUser BuyerUser { get; set; } = default!;
    public Guid? ParentId { get; set; }
    public Review? Parent { get; set; }
    public ICollection<Review> Replies { get; set; } = new List<Review>();
    public int Rating { get; set; }
    public string Text { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
