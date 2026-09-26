using Marketplace.Core.Common;

namespace Marketplace.Core.Entities;

public class ProductImage : Entity<Guid>
{
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;

    public int SortOrder { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
