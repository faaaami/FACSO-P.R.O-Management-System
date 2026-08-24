using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Documents.Commands.ConfirmDocument;

public sealed class ConfirmDocumentCommandHandler
    : IRequestHandler<
        ConfirmDocumentCommand,
        ConfirmDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;

    public ConfirmDocumentCommandHandler(
        IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ConfirmDocumentResponseDto> Handle(
        ConfirmDocumentCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Find extraction draft
        var extraction =
            await _context.DocumentExtractions
                .FirstOrDefaultAsync(
                    x => x.Id == request.ExtractionId,
                    cancellationToken);

        if (extraction is null)
        {
            throw new KeyNotFoundException(
                "Document extraction was not found.");
        }

        // 2. Do not allow an already processed extraction
        if (extraction.IsDeleted)
        {
            throw new InvalidOperationException(
                "Document extraction has been deleted.");
        }

        if (extraction.Status != "Pending")
        {
            throw new InvalidOperationException(
                $"Document extraction has already been processed. " +
                $"Current status: {extraction.Status}.");
        }

        // 3. Exactly one owner must be supplied
        var hasEmployee =
            request.EmployeeId.HasValue;

        var hasClientEntity =
            request.ClientEntityId.HasValue;

        if (hasEmployee == hasClientEntity)
        {
            throw new InvalidOperationException(
                "Exactly one of EmployeeId or ClientEntityId must be provided.");
        }

        // 4. Validate the owner exists
        if (request.EmployeeId.HasValue)
        {
            var employeeExists =
                await _context.Employees.AnyAsync(
                    x =>
                        x.Id == request.EmployeeId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

            if (!employeeExists)
            {
                throw new KeyNotFoundException(
                    "Employee was not found.");
            }
        }

        if (request.ClientEntityId.HasValue)
        {
            var entityExists =
                await _context.ClientEntities.AnyAsync(
                    x =>
                        x.Id == request.ClientEntityId.Value &&
                        !x.IsDeleted,
                    cancellationToken);

            if (!entityExists)
            {
                throw new KeyNotFoundException(
                    "Client entity was not found.");
            }
        }

        // 5. Validate dates
        if (request.ExpiryDate.HasValue &&
            request.IssueDate.HasValue &&
            request.ExpiryDate.Value <
            request.IssueDate.Value)
        {
            throw new InvalidOperationException(
                "Expiry date must be greater than or equal to issue date.");
        }

        // 6. Create the real document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            EmployeeId =
                request.EmployeeId,

            ClientEntityId =
                request.ClientEntityId,

            Type =
                request.Type,

            DocumentNumber =
                request.DocumentNumber.Trim(),

            IssueDate =
                request.IssueDate,

            ExpiryDate =
                request.ExpiryDate,

            FileUrl =
                request.FileUrl?.Trim(),

            FileName =
                request.FileName?.Trim(),

            ContentType =
                request.ContentType?.Trim(),

            FileSize =
                request.FileSize,

            IsActive = true,

            IsDeleted = false,

            DeletedAt = null,

            CreatedAt = now,

            UpdatedAt = now
        };

        _context.Documents.Add(document);

        // 7. Mark AI extraction as confirmed
        extraction.Status = "Confirmed";
        extraction.UpdatedAt = now;

        // 8. Save both changes
        await _context.SaveChangesAsync(
            cancellationToken);

        // 9. Return created document
        return new ConfirmDocumentResponseDto(
            document.Id,
            extraction.Id,
            document.EmployeeId,
            document.ClientEntityId,
            document.Type,
            document.DocumentNumber,
            document.IssueDate,
            document.ExpiryDate,
            document.FileUrl,
            document.FileName,
            document.ContentType,
            document.FileSize,
            document.IsActive,
            document.CreatedAt);
    }
}