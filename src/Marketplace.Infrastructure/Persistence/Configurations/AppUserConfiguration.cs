using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> e)
    {
        e.Property(x => x.DisplayName).HasMaxLength(80);
        e.Property(x => x.Email).HasMaxLength(256);
        e.HasMany(x => x.Addresses).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        e.HasMany(x => x.Wishlists).WithOne(x => x.User).HasForeignKey(x => x.UserId);
        e.HasMany(x => x.Notifications).WithOne(x => x.User).HasForeignKey(x => x.UserId);
    }
}
