using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> e)
    {
        e.HasIndex(x => x.Slug).IsUnique();
        e.Property(x => x.Name).HasMaxLength(120);
        e.Property(x => x.Slug).HasMaxLength(160);
        e.Property(x => x.Description).HasMaxLength(4000);
        e.Property(x => x.ImageUrl).HasMaxLength(500);
        e.Property(x => x.ContactEmail).HasMaxLength(256);
        e.Property(x => x.ContactPhone).HasMaxLength(30);
        e.Property(x => x.Region).HasMaxLength(120);
        e.Property(x => x.City).HasMaxLength(120);
        e.Property(x => x.Street).HasMaxLength(200);
        e.Property(x => x.PostalCode).HasMaxLength(20);
        e.Property(x => x.MetaTitle).HasMaxLength(160);
        e.Property(x => x.MetaDescription).HasMaxLength(500);
        e.Property(x => x.MetaImageUrl).HasMaxLength(500);
        e.HasOne(x => x.OwnerUser).WithOne(x => x.Store).HasForeignKey<Store>(x => x.OwnerUserId);
        e.HasOne(x => x.Finance).WithOne(x => x.Store).HasForeignKey<CompanyFinance>(x => x.StoreId);
        e.HasMany(x => x.Products).WithOne(x => x.Store).HasForeignKey(x => x.StoreId);
        e.HasMany(x => x.Schedules).WithOne(x => x.Store).HasForeignKey(x => x.StoreId);
        e.HasMany(x => x.Users).WithOne(x => x.Store).HasForeignKey(x => x.StoreId);
        e.HasMany(x => x.SellerRequests).WithOne(x => x.Store).HasForeignKey(x => x.StoreId).IsRequired(false);
        e.HasMany(x => x.Chats).WithOne(x => x.Store).HasForeignKey(x => x.StoreId);
    }
}
