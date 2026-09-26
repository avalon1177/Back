using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class ProductImageConfiguration : IEntityTypeConfiguration<ProductImage>
{
    public void Configure(EntityTypeBuilder<ProductImage> e)
    {
        e.Property(x => x.FileName).HasMaxLength(255);
        e.Property(x => x.ContentType).HasMaxLength(100);
        e.HasOne(x => x.Product).WithMany(x => x.Images).HasForeignKey(x => x.ProductId);
    }
}
