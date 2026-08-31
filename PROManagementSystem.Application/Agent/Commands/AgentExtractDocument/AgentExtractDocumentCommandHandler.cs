using MediatR;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.ExtractDocument;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentExtractDocument;

public sealed class AgentExtractDocumentCommandHandler
    : IRequestHandler<
        AgentExtractDocumentCommand,
        ExtractDocumentResponseDto>
{
    private readonly IDocumentExtractionService _extractor;
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;

    public AgentExtractDocumentCommandHandler(
        IDocumentExtractionService extractor,
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository)
    {
        _extractor = extractor;
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
    }

    public async Task<ExtractDocumentResponseDto> Handle(
        AgentExtractDocumentCommand request,
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

        // Ensure the caller is an authenticated agent.
        var agentId = _currentUser.UserId!.Value;

        var extractionResult =
            await _extractor.ExtractAsync(
                request.FileStream,
                request.ContentType,
                cancellationToken);

        var now = DateTime.UtcNow;

        var extraction = new DocumentExtraction
        {
            Id = Guid.NewGuid(),

            FileName = request.FileName,

            ContentType = request.ContentType,

            FileSize = request.FileSize,

            SuggestedType = extractionResult.Type,

            SuggestedDocumentNumber =
                extractionResult.DocumentNumber,

            SuggestedIssueDate =
                extractionResult.IssueDate,

            SuggestedExpiryDate =
                extractionResult.ExpiryDate,

            SuggestedOwnerType =
                extractionResult.OwnerType,

            Confidence = extractionResult.Confidence,

            Status = "Pending",

            CreatedAt = now,

            UpdatedAt = now,

            IsDeleted = false,

            DeletedAt = null,

            ExpiresAt = now.AddHours(24)
        };

        _context.DocumentExtractions.Add(extraction);

        await _context.SaveChangesAsync(
            cancellationToken);

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
