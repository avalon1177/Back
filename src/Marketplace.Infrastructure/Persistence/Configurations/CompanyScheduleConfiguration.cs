using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class CompanyScheduleConfiguration : IEntityTypeConfiguration<CompanySchedule>
{
    public void Configure(EntityTypeBuilder<CompanySchedule> e)
    {
        e.HasIndex(x => new { x.StoreId, x.Day }).IsUnique();
    }
}
