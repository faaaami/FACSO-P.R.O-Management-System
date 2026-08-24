using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Documents.Commands.UpdateDocument;

public sealed class UpdateDocumentCommandHandler
    : IRequestHandler<
        UpdateDocumentCommand,
        UpdateDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;

    public UpdateDocumentCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<UpdateDocumentResponseDto> Handle(
        UpdateDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var document =
            await _context.Documents
                .FirstOrDefaultAsync(
                    x => x.Id == request.DocumentId,
                    cancellationToken);

        if (document is null)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        if (document.IsDeleted)
        {
            throw new InvalidOperationException(
                "Cannot update a deleted document.");
        }

        if (request.ExpiryDate.HasValue &&
            request.IssueDate.HasValue &&
            request.ExpiryDate.Value < request.IssueDate.Value)
        {
            throw new InvalidOperationException(
                "Expiry date must be greater than or equal to issue date.");
        }

        document.Type = request.Type;

        document.DocumentNumber =
            request.DocumentNumber.Trim();

        document.IssueDate =
            request.IssueDate;

        document.ExpiryDate =
            request.ExpiryDate;

        document.FileUrl =
            request.FileUrl?.Trim();

        document.FileName =
            request.FileName?.Trim();

        document.ContentType =
            request.ContentType?.Trim();

        document.FileSize =
            request.FileSize;

        document.IsActive =
            request.IsActive;

        document.UpdatedAt =
            DateTime.UtcNow;

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateDocumentResponseDto(
            document.Id,
            document.ClientEntityId,
            document.EmployeeId,
            document.Type,
            document.DocumentNumber,
            document.IssueDate,
            document.ExpiryDate,
            document.FileUrl,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.IsActive,
            document.UpdatedAt);
    }
}