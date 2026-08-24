using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.ExtractDocument;

public sealed class ExtractDocumentCommandHandler
    : IRequestHandler<
        ExtractDocumentCommand,
        ExtractDocumentResponseDto>
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IApplicationDbContext _context;

    public ExtractDocumentCommandHandler(
        IDocumentExtractionService extractor,
        IApplicationDbContext context)
    {
        _extractor = extractor;
        _context = context;
    }

    public async Task<ExtractDocumentResponseDto> Handle(
        ExtractDocumentCommand request,
        CancellationToken cancellationToken)
    {
        if (request.FileStream is null)
        {
            throw new ArgumentNullException(
                nameof(request.FileStream));
        }

        if (request.FileSize <= 0)
        {
            throw new InvalidOperationException(
                "Uploaded file is empty.");
        }

        // 1. Send document to extraction service
        var extractionResult =
            await _extractor.ExtractAsync(
                request.FileStream,
                request.ContentType,
                cancellationToken);

        // 2. Create AI extraction draft
        var now = DateTime.UtcNow;

        var extraction = new DocumentExtraction
        {
            Id = Guid.NewGuid(),

            FileName =
                request.FileName,

            ContentType =
                request.ContentType,

            FileSize =
                request.FileSize,

            SuggestedType =
                extractionResult.Type,

            SuggestedDocumentNumber =
                extractionResult.DocumentNumber,

            SuggestedIssueDate =
                extractionResult.IssueDate,

            SuggestedExpiryDate =
                extractionResult.ExpiryDate,

            SuggestedOwnerType =
                extractionResult.OwnerType,

            Confidence =
                extractionResult.Confidence,

            Status =
                "Pending",

            CreatedAt =
                now,

            UpdatedAt =
                now,

            IsDeleted =
                false,

            DeletedAt =
                null,

            ExpiresAt =
                now.AddHours(24)
        };

        _context.DocumentExtractions.Add(
            extraction);

        await _context.SaveChangesAsync(
            cancellationToken);

        // 3. Return draft
        return new ExtractDocumentResponseDto(
            extraction.Id,
            extraction.SuggestedType,
            extraction.SuggestedDocumentNumber,
            extraction.SuggestedIssueDate,
            extraction.SuggestedExpiryDate,
            extraction.SuggestedOwnerType,
            extraction.Confidence);
    }
}