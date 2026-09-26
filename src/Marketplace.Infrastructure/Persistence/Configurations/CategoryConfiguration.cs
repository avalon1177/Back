using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> e)
    {
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Name).HasMaxLength(120);
        e.Property(x => x.Slug).HasMaxLength(160);
        e.Property(x => x.Description).HasMaxLength(2000);
        e.Property(x => x.ImageUrl).HasMaxLength(500);
        e.Property(x => x.MetaTitle).HasMaxLength(160);
        e.Property(x => x.MetaDescription).HasMaxLength(500);
        e.Property(x => x.MetaImageUrl).HasMaxLength(500);
        e.HasOne(x => x.Parent).WithMany(x => x.Children).HasForeignKey(x => x.ParentId).OnDelete(DeleteBehavior.Restrict);
    }
}
