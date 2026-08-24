using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentDependencyConfiguration
    : IEntityTypeConfiguration<DocumentDependency>
{
    public void Configure(
        EntityTypeBuilder<DocumentDependency> builder)
    {
        builder.ToTable("document_dependencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.DocumentId)
            .HasColumnName("document_id")
            .IsRequired();

        builder.Property(x => x.DependsOnDocumentId)
            .HasColumnName("depends_on_document_id")
            .IsRequired();

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

        // Document → Dependencies
        builder.HasOne(x => x.Document)
            .WithMany()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Dependency → Required Document
        builder.HasOne(x => x.DependsOnDocument)
            .WithMany()
            .HasForeignKey(x => x.DependsOnDocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Prevent duplicate dependencies
        builder.HasIndex(x => new
        {
            x.DocumentId,
            x.DependsOnDocumentId
        })
        .IsUnique();

        builder.HasIndex(x => x.DocumentId);

        builder.HasIndex(x => x.DependsOnDocumentId);
    }
}