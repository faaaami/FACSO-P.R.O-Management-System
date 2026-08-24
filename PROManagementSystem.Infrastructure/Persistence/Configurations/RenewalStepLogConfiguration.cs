using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class RenewalStepLogConfiguration
    : IEntityTypeConfiguration<RenewalStepLog>
{
    public void Configure(
        EntityTypeBuilder<RenewalStepLog> builder)
    {
        builder.ToTable("renewal_step_logs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StepName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ReferenceNumber)
            .HasMaxLength(200);

        builder.Property(x => x.ProofFileUrl)
            .HasMaxLength(2000);

        builder.HasOne(x => x.RenewalTask)
            .WithMany(x => x.StepLogs)
            .HasForeignKey(x => x.RenewalTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.CompletedByUser)
            .WithMany()
            .HasForeignKey(x => x.CompletedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RenewalTaskId);
    }
}