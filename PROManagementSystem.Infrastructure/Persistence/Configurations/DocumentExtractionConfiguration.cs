using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Infrastructure.Persistence.Configurations;

public sealed class DocumentExtractionConfiguration
    : IEntityTypeConfiguration<DocumentExtraction>
{
    public void Configure(
        EntityTypeBuilder<DocumentExtraction> builder)
    {
        builder.ToTable("document_extractions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FileName)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.FileUrl)
            .HasMaxLength(2000);

        builder.Property(x => x.ContentType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SuggestedDocumentNumber)
            .HasMaxLength(200);

        builder.Property(x => x.SuggestedOwnerType)
            .HasMaxLength(50);

        // Non-authoritative extraction/AI suggestions stored as a jsonb
        // document. System.Text.Json is used at the application layer to
        // (de)serialize the JSON object; EF stores the text verbatim. The
        // constant empty-object default means legacy rows are safely readable
        // ("{}") immediately after migration.
        builder.Property(x => x.SuggestedDetails)
            .HasColumnName("suggested_details")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(x => x.FieldResults)
            .HasColumnName("field_results")
            .HasColumnType("jsonb")
            .IsRequired()
            .HasDefaultValueSql("'{}'::jsonb");

        builder.Property(x => x.FailureCode)
            .HasColumnName("failure_code")
            .HasMaxLength(64);

        builder.Property(x => x.FailureMessage)
            .HasColumnName("failure_message")
            .HasMaxLength(500);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .IsRequired();

        builder.HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}