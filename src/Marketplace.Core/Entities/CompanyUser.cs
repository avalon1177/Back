using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class CompanyUser : Entity<Guid>
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public string RoleName { get; set; } = string.Empty;
    public bool IsOwner { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
