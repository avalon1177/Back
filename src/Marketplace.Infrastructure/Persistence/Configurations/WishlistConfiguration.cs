using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class WishlistConfiguration : IEntityTypeConfiguration<Wishlist>
{
    public void Configure(EntityTypeBuilder<Wishlist> e)
    {
        e.Property(x => x.Name).HasMaxLength(120);
        e.HasOne(x => x.User).WithMany(x => x.Wishlists).HasForeignKey(x => x.UserId);
    }
}
