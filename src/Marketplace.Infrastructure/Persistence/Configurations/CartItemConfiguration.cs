using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> e)
    {
        e.HasIndex(x => new { x.BuyerUserId, x.ProductId }).IsUnique();
        e.HasOne(x => x.BuyerUser).WithMany().HasForeignKey(x => x.BuyerUserId);
        e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId);
    }
}
