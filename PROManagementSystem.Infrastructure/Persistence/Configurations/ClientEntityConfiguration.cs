using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ClientEntityConfiguration
    : IEntityTypeConfiguration<ClientEntity>
{
    public void Configure(
        EntityTypeBuilder<ClientEntity> builder)
    {
        builder.ToTable("client_entities");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.ClientCompanyId)
            .HasColumnName("client_company_id")
            .IsRequired();

        builder.Property(x => x.EntityName)
            .HasColumnName("entity_name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TradeLicenseNumber)
            .HasColumnName("trade_license_number")
            .HasMaxLength(100);

        builder.Property(x => x.Emirate)
            .HasColumnName("emirate")
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne(x => x.ClientCompany)
            .WithMany(x => x.Entities)
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ClientCompanyId);

        builder.HasIndex(x => x.TradeLicenseNumber);
    }
}