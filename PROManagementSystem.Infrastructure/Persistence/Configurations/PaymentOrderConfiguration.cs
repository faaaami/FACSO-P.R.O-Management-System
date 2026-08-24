using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class PaymentOrderConfiguration
    : IEntityTypeConfiguration<PaymentOrder>
{
    public void Configure(
        EntityTypeBuilder<PaymentOrder> builder)
    {
        builder
            .ToTable("payment_orders");

        builder
            .HasKey(x => x.Id);

        builder
            .Property(x => x.InvoiceType)
            .IsRequired();

        builder
            .Property(x => x.Amount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder
            .Property(x => x.Currency)
            .HasMaxLength(10)
            .IsRequired();

        builder
            .Property(x => x.Status)
            .IsRequired();

        builder
            .Property(x => x.RazorpayOrderId)
            .HasMaxLength(200);

        builder
            .Property(x => x.RazorpayPaymentId)
            .HasMaxLength(200);

        builder
            .Property(x => x.RazorpaySignature)
            .HasMaxLength(500);

        builder
            .Property(x => x.Notes)
            .HasMaxLength(5000);

        builder
            .HasOne(x => x.RetainerInvoice)
            .WithMany()
            .HasForeignKey(x => x.RetainerInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.ServiceFeeInvoice)
            .WithMany()
            .HasForeignKey(x => x.ServiceFeeInvoiceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder
            .HasIndex(x => x.InvoiceType);

        builder
            .HasIndex(x => x.RetainerInvoiceId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder
            .HasIndex(x => x.ServiceFeeInvoiceId)
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");

        builder
            .HasIndex(x => x.ClientCompanyId);

        builder
            .HasIndex(x => x.Status);

        builder
            .HasIndex(x => x.RazorpayOrderId);
    }
}
