using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ServiceJobConfiguration : IEntityTypeConfiguration<ServiceJob>
{
    public void Configure(EntityTypeBuilder<ServiceJob> builder)
    {
        builder.ToTable("service_jobs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ServiceFee).HasPrecision(18, 2);
        builder.Property(x => x.EstimatedGovernmentFee).HasPrecision(18, 2);
        builder.Property(x => x.ApplicationNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(4000);
        builder.Property(x => x.OnHoldReason).HasMaxLength(1000);
        builder.Property(x => x.CancelReason).HasMaxLength(1000);

        builder.HasOne(x => x.ClientCompany).WithMany()
            .HasForeignKey(x => x.ClientCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ServiceDefinition).WithMany()
            .HasForeignKey(x => x.ServiceDefinitionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Employee).WithMany()
            .HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ClientEntity).WithMany()
            .HasForeignKey(x => x.ClientEntityId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Document).WithMany()
            .HasForeignKey(x => x.DocumentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.AssignedStaff).WithMany()
            .HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(x => x.Steps).WithOne(x => x.ServiceJob)
            .HasForeignKey(x => x.ServiceJobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Documents).WithOne(x => x.ServiceJob)
            .HasForeignKey(x => x.ServiceJobId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(x => x.Activities).WithOne(x => x.ServiceJob)
            .HasForeignKey(x => x.ServiceJobId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.JobNumber).IsUnique();
        builder.HasIndex(x => x.ClientCompanyId);
        builder.HasIndex(x => x.AssignedStaffId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ServiceDefinitionId);
        builder.HasIndex(x => x.EmployeeId);
    }
}

public sealed class ServiceJobStepConfiguration : IEntityTypeConfiguration<ServiceJobStep>
{
    public void Configure(EntityTypeBuilder<ServiceJobStep> builder)
    {
        builder.ToTable("service_job_steps");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ReferenceNumber).HasMaxLength(100);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => x.ServiceJobId);
    }
}

public sealed class ServiceJobDocumentConfiguration : IEntityTypeConfiguration<ServiceJobDocument>
{
    public void Configure(EntityTypeBuilder<ServiceJobDocument> builder)
    {
        builder.ToTable("service_job_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.ServiceJobId);
    }
}

public sealed class ServiceJobActivityConfiguration : IEntityTypeConfiguration<ServiceJobActivity>
{
    public void Configure(EntityTypeBuilder<ServiceJobActivity> builder)
    {
        builder.ToTable("service_job_activities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Action).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(2000);
        builder.HasIndex(x => x.ServiceJobId);
    }
}
