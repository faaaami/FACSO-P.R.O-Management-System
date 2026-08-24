using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentVersionConfiguration
    : IEntityTypeConfiguration<DocumentVersion>
{
    public void Configure(
        EntityTypeBuilder<DocumentVersion> builder)
    {
        builder.ToTable("document_versions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(x => x.VersionNumber)
            .HasColumnName("version_number")
            .IsRequired();

        builder.Property(x => x.FileUrl)
            .HasColumnName("file_url")
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(x => x.FileSize)
            .HasColumnName("file_size");

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

        builder.HasOne(x => x.Document)
            .WithMany(x => x.Versions)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.DocumentId);

        // Prevent duplicate versions for the same document
        builder.HasIndex(
            x => new
            {
                x.DocumentId,
                x.VersionNumber
            })
            .IsUnique();
    }
}