using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class UserRefreshToken : Entity<Guid>
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public string Token { get; set; } = string.Empty;
    public string Purpose { get; set; } = "refresh";
    public string? DeviceId { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByToken { get; set; }
}
