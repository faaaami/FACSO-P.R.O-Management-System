using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class EmployeeConfiguration
    : IEntityTypeConfiguration<Employee>
{
    public void Configure(
        EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable("employees");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ClientEntityId)
            .HasColumnName("client_entity_id")
            .IsRequired();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.PassportNumber)
            .HasColumnName("passport_number")
            .HasMaxLength(50);

        builder.Property(x => x.DateOfBirth)
            .HasColumnName("date_of_birth");

        builder.Property(x => x.Nationality)
            .HasColumnName("nationality")
            .HasMaxLength(100);

        builder.Property(x => x.JobTitle)
            .HasColumnName("job_title")
            .HasMaxLength(150);

        builder.Property(x => x.HireDate)
            .HasColumnName("hire_date")
            .IsRequired();

        builder.Property(x => x.PersonType)
            .HasColumnName("person_type")
            .HasDefaultValue(PROManagementSystem.Domain.Enums.PersonType.Employee)
            .IsRequired();

        builder.Property(x => x.FullNameArabic)
            .HasColumnName("full_name_arabic")
            .HasMaxLength(200);

        builder.Property(x => x.Gender)
            .HasColumnName("gender")
            .HasMaxLength(1);

        builder.Property(x => x.Mobile)
            .HasColumnName("mobile")
            .HasMaxLength(30);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .HasMaxLength(320);

        builder.Property(x => x.EmiratesIdNumber)
            .HasColumnName("emirates_id_number")
            .HasMaxLength(20);

        builder.Property(x => x.UidNumber)
            .HasColumnName("uid_number")
            .HasMaxLength(30);

        builder.Property(x => x.MohrePersonCode)
            .HasColumnName("mohre_person_code")
            .HasMaxLength(30);

        builder.Property(x => x.SponsorId)
            .HasColumnName("sponsor_id");

        builder.Property(x => x.RelationshipToSponsor)
            .HasColumnName("relationship_to_sponsor")
            .HasMaxLength(50);

        builder.HasOne(x => x.Sponsor)
            .WithMany()
            .HasForeignKey(x => x.SponsorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.EmiratesIdNumber);
        builder.HasIndex(x => x.PassportNumber);
        builder.HasIndex(x => x.SponsorId);

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);

        builder.Property(x => x.DeletedAt)
            .HasColumnName("deleted_at");

        // ClientEntity → Employees
        builder.HasOne(x => x.ClientEntity)
            .WithMany(x => x.Employees)
            .HasForeignKey(x => x.ClientEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Useful for filtering employees by entity
        builder.HasIndex(x => x.ClientEntityId);

        // Useful for searching/filtering
        builder.HasIndex(x => x.IsActive);

        builder.HasIndex(x => x.IsDeleted);
    }
}