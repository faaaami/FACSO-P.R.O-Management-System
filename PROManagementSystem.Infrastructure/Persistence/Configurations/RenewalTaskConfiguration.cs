using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class RenewalTaskConfiguration
    : IEntityTypeConfiguration<RenewalTask>
{
    public void Configure(
        EntityTypeBuilder<RenewalTask> builder)
    {
        builder.ToTable("renewal_tasks");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.BlockedReason)
            .HasMaxLength(1000);

        // Document
        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Client Company
        builder.HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Assigned Agent
        builder.HasOne(x => x.AssignedStaff)
            .WithMany()
            .HasForeignKey(x => x.AssignedStaffId)
            .OnDelete(DeleteBehavior.SetNull);

        // Step logs
        builder.HasMany(x => x.StepLogs)
            .WithOne(x => x.RenewalTask)
            .HasForeignKey(x => x.RenewalTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // History
        builder.HasMany(x => x.History)
            .WithOne(x => x.RenewalTask)
            .HasForeignKey(x => x.RenewalTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DocumentId);
        builder.HasIndex(x => x.ClientCompanyId);
        builder.HasIndex(x => x.AssignedStaffId);
        builder.HasIndex(x => x.Status);
    }
}