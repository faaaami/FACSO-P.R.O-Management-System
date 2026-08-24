using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class RetainerInvoiceConfiguration
    : IEntityTypeConfiguration<RetainerInvoice>
{
    public void Configure(
        EntityTypeBuilder<RetainerInvoice> builder)
    {
        builder.ToTable("retainer_invoices");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InvoiceNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(x => x.Notes)
            .HasMaxLength(5000);

        builder.Property(x => x.VoidReason)
            .HasMaxLength(5000);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ServiceContract)
            .WithMany()
            .HasForeignKey(x => x.ServiceContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClientCompanyId);

        builder.HasIndex(x => x.ServiceContractId);

        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.InvoiceNumber)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
