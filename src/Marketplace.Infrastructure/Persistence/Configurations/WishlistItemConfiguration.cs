using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class WishlistItemConfiguration : IEntityTypeConfiguration<WishlistItem>
{
    public void Configure(EntityTypeBuilder<WishlistItem> e)
    {
        e.HasIndex(x => new { x.WishlistId, x.ProductId }).IsUnique();
        e.HasOne(x => x.Wishlist).WithMany(x => x.Items).HasForeignKey(x => x.WishlistId);
        e.HasOne(x => x.Product).WithMany(x => x.WishlistItems).HasForeignKey(x => x.ProductId);
    }
}
