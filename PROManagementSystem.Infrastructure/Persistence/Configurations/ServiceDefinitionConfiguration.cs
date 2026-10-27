using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ServiceDefinitionConfiguration : IEntityTypeConfiguration<ServiceDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceDefinition> builder)
    {
        builder.ToTable("service_definitions");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Code).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NameArabic).HasMaxLength(200);
        builder.Property(x => x.Authority).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.DefaultServiceFee).HasPrecision(18, 2);
        builder.Property(x => x.EstimatedGovernmentFee).HasPrecision(18, 2);

        builder.HasMany(x => x.Steps)
            .WithOne(x => x.ServiceDefinition)
            .HasForeignKey(x => x.ServiceDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.RequiredDocuments)
            .WithOne(x => x.ServiceDefinition)
            .HasForeignKey(x => x.ServiceDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.Code).IsUnique().HasFilter("\"IsDeleted\" = false");
        builder.HasIndex(x => x.Category);
    }
}

public sealed class ServiceStepTemplateConfiguration : IEntityTypeConfiguration<ServiceStepTemplate>
{
    public void Configure(EntityTypeBuilder<ServiceStepTemplate> builder)
    {
        builder.ToTable("service_step_templates");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.HasIndex(x => x.ServiceDefinitionId);
    }
}

public sealed class ServiceRequiredDocumentConfiguration : IEntityTypeConfiguration<ServiceRequiredDocument>
{
    public void Configure(EntityTypeBuilder<ServiceRequiredDocument> builder)
    {
        builder.ToTable("service_required_documents");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.ServiceDefinitionId);
    }
}

public sealed class ContractServicePriceConfiguration : IEntityTypeConfiguration<ContractServicePrice>
{
    public void Configure(EntityTypeBuilder<ContractServicePrice> builder)
    {
        builder.ToTable("contract_service_prices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ServiceFee).HasPrecision(18, 2);

        builder.HasOne(x => x.ServiceContract)
            .WithMany()
            .HasForeignKey(x => x.ServiceContractId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ServiceDefinition)
            .WithMany()
            .HasForeignKey(x => x.ServiceDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => new { x.ServiceContractId, x.ServiceDefinitionId })
            .IsUnique()
            .HasFilter("\"IsDeleted\" = false");
    }
}
