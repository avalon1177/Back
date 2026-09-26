using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class Store : Entity<Guid>
{
    public Guid OwnerUserId { get; set; }
    public AppUser OwnerUser { get; set; } = default!;

    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string ContactEmail { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public bool IsFeatured { get; set; }
    public bool IsApproved { get; set; } = true;
    public Guid? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    public string? MetaImageUrl { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }

    public CompanyFinance? Finance { get; set; }
    public ICollection<Product> Products { get; set; } = new List<Product>();
    public ICollection<CompanySchedule> Schedules { get; set; } = new List<CompanySchedule>();
    public ICollection<CompanyUser> Users { get; set; } = new List<CompanyUser>();
    public ICollection<SellerRequest> SellerRequests { get; set; } = new List<SellerRequest>();
    public ICollection<Chat> Chats { get; set; } = new List<Chat>();
}
