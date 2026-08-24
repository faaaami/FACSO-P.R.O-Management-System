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

        builder.Property(x => x.Confidence)
            .HasPrecision(5, 4);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasMaxLength(30);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired();

        builder.Property(x => x.IsDeleted)
            .IsRequired();
    }
}