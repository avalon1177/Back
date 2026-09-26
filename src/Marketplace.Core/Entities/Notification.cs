using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Notification : Entity<Guid>
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
