using Marketplace.Core.Common;
using Marketplace.Core.Enums;

namespace Marketplace.Core.Entities;

public class SellerRequest : Entity<Guid>
{
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = default!;
    public Guid? StoreId { get; set; }
    public Store? Store { get; set; }
    public string AdditionalInformation { get; set; } = string.Empty;
    public SellerRequestStatus Status { get; set; } = SellerRequestStatus.Pending;
    public DateTime? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public Guid? RejectedByUserId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
