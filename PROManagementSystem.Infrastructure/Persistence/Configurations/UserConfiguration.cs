using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id")
            .ValueGeneratedOnAdd();

        builder.Property(x => x.FullName)
            .HasColumnName("full_name")
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Email)
            .HasColumnName("email")
            .IsRequired()
            .HasMaxLength(255);

        builder.HasIndex(x => x.Email)
            .IsUnique();

        builder.Property(x => x.PasswordHash)
            .HasColumnName("password_hash")
            .IsRequired();

        builder.Property(x => x.Phone)
            .HasColumnName("phone")
            .HasMaxLength(30);

        builder.Property(x => x.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true);

        builder.Property(x => x.RefreshToken)
            .HasColumnName("refresh_token");

        builder.Property(x => x.RefreshTokenExpiry)
            .HasColumnName("refresh_token_expiry");

        builder.Property(x => x.IsApproved)
    .HasColumnName("is_approved")
    .HasDefaultValue(false);

      
        builder.Property(x => x.EmailVerified)
            .HasColumnName("email_verified")
            .HasDefaultValue(false);

        builder.Property(x => x.ClientCompanyId)
            .HasColumnName("client_company_id");

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
    }
}