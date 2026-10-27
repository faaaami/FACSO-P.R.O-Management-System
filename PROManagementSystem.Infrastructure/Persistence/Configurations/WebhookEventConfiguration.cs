using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class WebhookEventConfiguration : IEntityTypeConfiguration<WebhookEvent>
{
    public const string EventIdIndexName = "IX_webhook_events_EventId";

    public void Configure(EntityTypeBuilder<WebhookEvent> builder)
    {
        builder.ToTable("webhook_events");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventId).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EventType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.RazorpayOrderId).HasMaxLength(200);
        builder.Property(x => x.RazorpayPaymentId).HasMaxLength(200);
        builder.Property(x => x.Outcome).HasMaxLength(50).IsRequired();

        // Deliberately not filtered by IsDeleted: an event id, once seen, is
        // never processed again.
        builder.HasIndex(x => x.EventId, EventIdIndexName).IsUnique();
        builder.HasIndex(x => x.RazorpayOrderId);
    }
}
