using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Application.Documents.Commands.UpdateDocument;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentUpdateDocument;

public sealed class AgentUpdateDocumentCommandHandler
    : IRequestHandler<
        AgentUpdateDocumentCommand,
        UpdateDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;
    private readonly DocumentDetailsValidator _detailsValidator;

    public AgentUpdateDocumentCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository,
        DocumentDetailsValidator detailsValidator)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
        _detailsValidator = detailsValidator;
    }

    public async Task<UpdateDocumentResponseDto> Handle(
        AgentUpdateDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

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

        // Strict task-linked scope check. Out-of-scope -> 404.
        var documentInScope =
            await _scopeRepository.IsAgentDocumentInScopeAsync(
                agentId,
                request.DocumentId,
                cancellationToken);

        if (!documentInScope)
        {
            throw new KeyNotFoundException(
                "Document was not found.");
        }

        if (request.ExpiryDate.HasValue &&
            request.IssueDate.HasValue &&
            request.ExpiryDate.Value < request.IssueDate.Value)
        {
            throw new InvalidOperationException(
                "Expiry date must be greater than or equal to issue date.");
        }

        // Agents may only edit the explicit allowed field subset.
        // Type and IsActive are intentionally not mutated here.
        //
        // PATCH is partial: only properties supplied in the request body
        // are applied. Omitted properties are left unchanged.
        var present = request.PresentProperties;

        if (present.Contains("DocumentNumber"))
        {
            // Task 7B.6.4: agents may change the document number, so validate
            // the resulting owner/type/number combination against other active
            // documents. Self-exclusion keeps a no-op number update valid.
            await DocumentDuplicateGuard.EnsureNotDuplicateAsync(
                _context,
                document.EmployeeId,
                document.ClientEntityId,
                document.Type,
                request.DocumentNumber,
                cancellationToken,
                document.Id);

            document.DocumentNumber =
                request.DocumentNumber.Trim();
        }

        if (present.Contains("IssueDate"))
        {
            document.IssueDate =
                request.IssueDate;
        }

        if (present.Contains("ExpiryDate"))
        {
            document.ExpiryDate =
                request.ExpiryDate;
        }

        if (present.Contains("FileUrl"))
        {
            document.FileUrl =
                request.FileUrl?.Trim();
        }

        if (present.Contains("FileName"))
        {
            document.FileName =
                request.FileName?.Trim();
        }

        if (present.Contains("ContentType"))
        {
            document.ContentType =
                request.ContentType?.Trim();
        }

        if (present.Contains("FileSize"))
        {
            document.FileSize =
                request.FileSize;
        }

        // Details is whole-object replacement: a supplied object replaces the
        // stored value, a supplied null resets it to the empty object, and an
        // omitted property leaves it unchanged. Type-specific schema validation
        // matches the stored document's Type, which the PATCH command does not
        // carry (Task 7B.5).
        if (present.Contains("Details"))
        {
            var failures = _detailsValidator.Validate(
                document.Type,
                request.Details,
                validateRequired: false);

            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }

            document.Details =
                DetailsJson.ToStorage(request.Details);
        }

        document.UpdatedAt =
            DateTime.UtcNow;

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = agentId,
            Action = "DocumentUpdated",
            EntityType = "Document",
            EntityId = document.Id,
            Description =
                $"Document '{document.FileName ?? document.DocumentNumber}' was updated.",
            CreatedAt = document.UpdatedAt,
            UpdatedAt = document.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

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
            document.UpdatedAt,
            DetailsJson.FromStorage(document.Details));
    }
}
