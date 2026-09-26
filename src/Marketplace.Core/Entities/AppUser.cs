using Microsoft.AspNetCore.Identity;

namespace Marketplace.Core.Entities;

public class AppUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime? Birthday { get; set; }
    public DateTime? LastSeenAtUtc { get; set; }
    public bool IsApproved { get; set; } = true;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public Store? Store { get; set; }
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Wishlist> Wishlists { get; set; } = new List<Wishlist>();
    public ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public ICollection<UserRefreshToken> RefreshTokens { get; set; } = new List<UserRefreshToken>();
}
