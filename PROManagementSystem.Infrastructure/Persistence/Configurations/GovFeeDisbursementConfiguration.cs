using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class GovFeeDisbursementConfiguration
    : IEntityTypeConfiguration<GovFeeDisbursement>
{
    public void Configure(
        EntityTypeBuilder<GovFeeDisbursement> builder)
    {
        builder
            .ToTable("gov_fee_disbursements");

        builder
            .HasKey(x => x.Id);

        builder
            .Property(x => x.FeeDescription)
            .HasMaxLength(2000)
            .IsRequired();

        builder
            .Property(x => x.GovernmentReference)
            .HasMaxLength(500);

        builder
            .Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder
            .Property(x => x.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder
            .Property(x => x.Notes)
            .HasMaxLength(5000);

        builder
            .Property(x => x.Status)
            .IsRequired();

        builder
            .HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.RenewalTask)
            .WithMany()
            .HasForeignKey(x => x.RenewalTaskId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.ClientCompanyId);

        builder
            .HasIndex(x => x.RenewalTaskId);

        builder
            .HasOne(x => x.ServiceJob)
            .WithMany()
            .HasForeignKey(x => x.ServiceJobId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.ServiceJobId);

        builder
            .HasIndex(x => x.Status);
    }
}
