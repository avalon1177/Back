using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> e)
    {
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Title).HasMaxLength(160);
        e.Property(x => x.Slug).HasMaxLength(180);
        e.Property(x => x.Description).HasMaxLength(6000);
        e.Property(x => x.Price).HasPrecision(18, 2);
        e.Property(x => x.AttributesJson).HasColumnType("jsonb");
        e.Property(x => x.MetaTitle).HasMaxLength(160);
        e.Property(x => x.MetaDescription).HasMaxLength(500);
        e.Property(x => x.MetaImageUrl).HasMaxLength(500);
        e.HasOne(x => x.Store).WithMany(x => x.Products).HasForeignKey(x => x.StoreId);
        e.HasOne(x => x.Category).WithMany(x => x.Products).HasForeignKey(x => x.CategoryId);
    }
}
