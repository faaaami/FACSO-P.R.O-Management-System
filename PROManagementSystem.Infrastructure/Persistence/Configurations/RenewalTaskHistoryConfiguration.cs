using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class RenewalTaskHistoryConfiguration
    : IEntityTypeConfiguration<RenewalTaskHistory>
{
    public void Configure(
        EntityTypeBuilder<RenewalTaskHistory> builder)
    {
        builder.ToTable("renewal_task_histories");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Note)
            .HasMaxLength(1000);

        builder.HasOne(x => x.RenewalTask)
            .WithMany(x => x.History)
            .HasForeignKey(x => x.RenewalTaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ChangedByUser)
            .WithMany()
            .HasForeignKey(x => x.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RenewalTaskId);
        builder.HasIndex(x => x.ChangedAt);
    }
}