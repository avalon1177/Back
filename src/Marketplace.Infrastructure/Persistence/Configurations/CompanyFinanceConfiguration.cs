using Marketplace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Infrastructure.Persistence.Configurations;

public class CompanyFinanceConfiguration : IEntityTypeConfiguration<CompanyFinance>
{
    public void Configure(EntityTypeBuilder<CompanyFinance> e)
    {
        e.Property(x => x.BankAccount).HasMaxLength(64);
        e.Property(x => x.BankName).HasMaxLength(120);
        e.Property(x => x.BankCode).HasMaxLength(32);
        e.Property(x => x.TaxId).HasMaxLength(32);
        e.Property(x => x.PaymentDetails).HasMaxLength(500);
    }
}
