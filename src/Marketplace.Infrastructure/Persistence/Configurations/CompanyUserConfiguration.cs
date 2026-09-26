using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class CompanyUserConfiguration : IEntityTypeConfiguration<CompanyUser>
{
    public void Configure(EntityTypeBuilder<CompanyUser> e)
    {
        e.HasIndex(x => new { x.StoreId, x.UserId }).IsUnique();
        e.Property(x => x.RoleName).HasMaxLength(64);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
