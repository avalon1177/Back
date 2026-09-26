using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class CompanyFinance : Entity<Guid>
{
    public Guid StoreId { get; set; }
    public Store Store { get; set; } = default!;
    public string BankAccount { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string BankCode { get; set; } = string.Empty;
    public string TaxId { get; set; } = string.Empty;
    public string PaymentDetails { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
}
