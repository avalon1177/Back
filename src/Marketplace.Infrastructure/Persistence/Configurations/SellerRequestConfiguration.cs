using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class SellerRequestConfiguration : IEntityTypeConfiguration<SellerRequest>
{
    public void Configure(EntityTypeBuilder<SellerRequest> e)
    {
        e.Property(x => x.AdditionalInformation).HasMaxLength(2000);
        e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId);
    }
}
