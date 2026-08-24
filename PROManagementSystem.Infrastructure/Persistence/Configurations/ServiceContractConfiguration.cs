using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ServiceContractConfiguration
    : IEntityTypeConfiguration<ServiceContract>
{
    public void Configure(
        EntityTypeBuilder<ServiceContract> builder)
    {
        builder.ToTable("service_contracts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ContractNumber)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.RetainerAmount)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.Terms)
            .HasMaxLength(5000);

        builder.Property(x => x.Status)
            .IsRequired();

        builder.HasOne(x => x.ClientCompany)
            .WithMany()
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClientCompanyId);

        builder.HasIndex(x => x.ContractNumber)
            .IsUnique();

        builder.HasIndex(x => x.Status);
    }
}