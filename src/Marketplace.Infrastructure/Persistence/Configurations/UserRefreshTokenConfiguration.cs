using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class UserRefreshTokenConfiguration : IEntityTypeConfiguration<UserRefreshToken>
{
    public void Configure(EntityTypeBuilder<UserRefreshToken> b)
    {
        b.ToTable("UserRefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(300).IsRequired();
        b.Property(x => x.Purpose).HasMaxLength(32).IsRequired();
        b.Property(x => x.DeviceId).HasMaxLength(128);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Purpose, x.IsRevoked });
        b.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
