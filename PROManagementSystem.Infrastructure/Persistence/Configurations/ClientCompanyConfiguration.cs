using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class ClientCompanyConfiguration
    : IEntityTypeConfiguration<ClientCompany>
{
    public void Configure(
        EntityTypeBuilder<ClientCompany> builder)
    {
        builder.ToTable("client_companies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(x => x.CompanyName)
            .HasColumnName("company_name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TradeLicenseNumber)
            .HasColumnName("trade_license_number")
            .HasMaxLength(100);

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(x => x.Address)
            .HasColumnName("address")
            .HasMaxLength(500);

        builder.Property(x => x.Emirate)
            .HasColumnName("emirate")
            .HasMaxLength(100);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(x => x.CustomerType)
            .HasColumnName("customer_type")
            .HasDefaultValue(PROManagementSystem.Domain.Enums.CustomerType.Subscription)
            .IsRequired();

        builder.Property(x => x.CustomerCode)
            .HasColumnName("customer_code")
            .HasMaxLength(20);

        builder.Property(x => x.TaxRegistrationNumber)
            .HasColumnName("tax_registration_number")
            .HasMaxLength(15);

        builder.Property(x => x.ContactPersonName)
            .HasColumnName("contact_person_name")
            .HasMaxLength(200);

        builder.Property(x => x.Notes)
            .HasColumnName("notes")
            .HasMaxLength(2000);

        builder.HasIndex(x => x.CustomerCode)
            .IsUnique()
            .HasFilter("customer_code IS NOT NULL");

        builder.HasIndex(x => x.CustomerType);

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

        // ClientCompany → ClientEntity
        builder.HasMany(x => x.Entities)
            .WithOne(x => x.ClientCompany)
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        // ClientCompany → User contacts
        builder.HasMany(x => x.Contacts)
            .WithOne(x => x.ClientCompany)
            .HasForeignKey(x => x.ClientCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}