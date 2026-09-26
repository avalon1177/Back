using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> e)
    {
        e.HasIndex(x => new { x.ProductId, x.BuyerUserId }).IsUnique();
        e.Property(x => x.Text).HasMaxLength(2000);
        e.HasOne(x => x.Product).WithMany(x => x.Reviews).HasForeignKey(x => x.ProductId);
        e.HasOne(x => x.BuyerUser).WithMany().HasForeignKey(x => x.BuyerUserId);
        e.HasOne(x => x.Parent).WithMany(x => x.Replies).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
