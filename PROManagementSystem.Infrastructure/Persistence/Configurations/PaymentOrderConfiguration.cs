using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class PaymentOrderConfiguration
    : IEntityTypeConfiguration<PaymentOrder>
{
    public const string OpenRetainerOrderIndexName =
        "IX_payment_orders_RetainerInvoiceId_Open";

    public const string OpenServiceFeeOrderIndexName =
        "IX_payment_orders_ServiceFeeInvoiceId_Open";

    private const string OpenOrderFilter =
        "\"IsDeleted\" = false AND \"Status\" IN (1, 2)";

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
            .Property(x => x.CapturedAmount)
            .HasPrecision(18, 2);

        builder
            .Property(x => x.CapturedCurrency)
            .HasMaxLength(10);

        builder
            .Property(x => x.ReviewReason)
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
            .HasIndex(x => x.RetainerInvoiceId);

        builder
            .HasIndex(x => x.ServiceFeeInvoiceId);

        // An invoice may have many payment orders over time (cancelled,
        // failed, paid), but at most ONE open order (Created = 1,
        // Pending = 2) at any moment.
        builder
            .HasIndex(x => x.RetainerInvoiceId, OpenRetainerOrderIndexName)
            .IsUnique()
            .HasFilter(OpenOrderFilter);

        builder
            .HasIndex(x => x.ServiceFeeInvoiceId, OpenServiceFeeOrderIndexName)
            .IsUnique()
            .HasFilter(OpenOrderFilter);

        builder
            .HasIndex(x => x.ClientCompanyId);

        builder
            .HasIndex(x => x.Status);

        builder
            .HasIndex(x => x.RazorpayOrderId);
    }
}
