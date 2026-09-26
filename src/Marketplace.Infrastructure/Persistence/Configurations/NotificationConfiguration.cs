using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.Property(x => x.Title).HasMaxLength(160);
        e.Property(x => x.Text).HasMaxLength(2000);
        e.HasOne(x => x.User).WithMany(x => x.Notifications).HasForeignKey(x => x.UserId);
    }
}
