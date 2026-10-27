using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ServiceRequestConfiguration
    : IEntityTypeConfiguration<ServiceRequest>
{
    public void Configure(
        EntityTypeBuilder<ServiceRequest> builder)
    {
        builder.ToTable("service_requests");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.Property(x => x.Type)
            .IsRequired();

        builder.Property(x => x.Description)
            .HasMaxLength(2000);

        builder.Property(x => x.RejectionReason)
            .HasMaxLength(1000);

        builder.HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Entity)
            .WithMany()
            .HasForeignKey(x => x.EntityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConvertedRenewalTask)
            .WithMany()
            .HasForeignKey(x => x.ConvertedRenewalTaskId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ServiceDefinition)
            .WithMany()
            .HasForeignKey(x => x.ServiceDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ConvertedServiceJob)
            .WithMany()
            .HasForeignKey(x => x.ConvertedServiceJobId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.ClientCompanyId);

        builder.HasIndex(x => x.EmployeeId);

        builder.HasIndex(x => x.EntityId);

        builder.HasIndex(x => x.DocumentId);

        builder.HasIndex(x => x.Status);

        builder.ToTable(x => x.HasCheckConstraint(
            "CK_service_requests_subject",
            "\"EmployeeId\" IS NOT NULL OR \"EntityId\" IS NOT NULL"));
    }
}