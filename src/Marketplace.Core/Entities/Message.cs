using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Message : Entity<Guid>
{
    public Guid ChatId { get; set; }
    public Chat Chat { get; set; } = default!;
    public Guid SenderId { get; set; }
    public AppUser Sender { get; set; } = default!;
    public string MessageText { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
