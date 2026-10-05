using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration
    : IEntityTypeConfiguration<Document>
{
    public void Configure(
        EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.ClientEntityId)
            .HasColumnName("client_entity_id");

        builder.Property(x => x.EmployeeId)
            .HasColumnName("employee_id");

        builder.Property(x => x.Type)
            .HasColumnName("type")
            .HasConversion<string>()
            .IsRequired();

        builder.Property(x => x.DocumentNumber)
            .HasColumnName("document_number")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(x => x.IssueDate)
            .HasColumnName("issue_date");

        builder.Property(x => x.ExpiryDate)
            .HasColumnName("expiry_date");

        builder.Property(x => x.FileUrl)
            .HasColumnName("file_url")
            .HasMaxLength(1000);

        builder.Property(x => x.FileName)
            .HasColumnName("file_name")
            .HasMaxLength(255);

        builder.Property(x => x.ContentType)
            .HasColumnName("content_type")
            .HasMaxLength(100);

        builder.Property(x => x.FileSize)
            .HasColumnName("file_size");

        // Type-specific details stored as a jsonb document. System.Text.Json is
        // used at the application layer to (de)serialize the JSON object; EF
        // stores the text verbatim. The constant empty-object default means
        // legacy rows are safely readable ("{}") immediately after migration.
        builder.Property(x => x.Details)
            .HasColumnName("details")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb");

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

        // ClientEntity -> Documents
        builder.HasOne(x => x.ClientEntity)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.ClientEntityId)
            .OnDelete(DeleteBehavior.Restrict);

        // Employee -> Documents
        builder.HasOne(x => x.Employee)
            .WithMany(x => x.Documents)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // Document -> Versions
        builder.HasMany(x => x.Versions)
            .WithOne(x => x.Document)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Indexes
        builder.HasIndex(x => x.ClientEntityId);

        builder.HasIndex(x => x.EmployeeId);

        builder.HasIndex(x => x.ExpiryDate);

        builder.HasIndex(x => x.IsDeleted);

        // Exactly one owner:
        // ClientEntity OR Employee
        builder.ToTable(
            "documents",
            table =>
                table.HasCheckConstraint(
                    "CK_documents_exactly_one_owner",
                    """
                    (
                        (client_entity_id IS NOT NULL AND employee_id IS NULL)
                        OR
                        (client_entity_id IS NULL AND employee_id IS NOT NULL)
                    )
                    """));
    }
}