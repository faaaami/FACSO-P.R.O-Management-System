using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Commands.ConfirmDocument;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Agent.Commands.AgentConfirmExtraction;

public sealed class AgentConfirmExtractionCommandHandler
    : IRequestHandler<
        AgentConfirmExtractionCommand,
        ConfirmDocumentResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IRenewalTaskReadRepository _scopeRepository;

    public AgentConfirmExtractionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IRenewalTaskReadRepository scopeRepository)
    {
        _context = context;
        _currentUser = currentUser;
        _scopeRepository = scopeRepository;
    }

    public async Task<ConfirmDocumentResponseDto> Handle(
        AgentConfirmExtractionCommand request,
        CancellationToken cancellationToken)
    {
        var agentId = _currentUser.UserId!.Value;

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

        // 2. Exactly one owner must be supplied
        var hasEmployee = request.EmployeeId.HasValue;
        var hasClientEntity = request.ClientEntityId.HasValue;

        if (hasEmployee == hasClientEntity)
        {
            throw new InvalidOperationException(
                "Exactly one of EmployeeId or ClientEntityId must be provided.");
        }

        // 3. Verify the selected owner is within the agent's
        //    strict task-linked scope. Out-of-scope -> 404.
        if (request.EmployeeId.HasValue)
        {
            var employeeInScope =
                await _scopeRepository.IsAgentEmployeeInScopeAsync(
                    agentId,
                    request.EmployeeId.Value,
                    cancellationToken);

            if (!employeeInScope)
            {
                throw new KeyNotFoundException(
                    "Employee was not found.");
            }
        }

        if (request.ClientEntityId.HasValue)
        {
            var entityInScope =
                await _scopeRepository.IsAgentEntityInScopeAsync(
                    agentId,
                    request.ClientEntityId.Value,
                    cancellationToken);

            if (!entityInScope)
            {
                throw new KeyNotFoundException(
                    "Client entity was not found.");
            }
        }

        // 4. Validate the owner still exists and is active
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
            request.ExpiryDate.Value < request.IssueDate.Value)
        {
            throw new InvalidOperationException(
                "Expiry date must be greater than or equal to issue date.");
        }

        // 6. Create the real document
        var now = DateTime.UtcNow;

        var document = new Document
        {
            Id = Guid.NewGuid(),

            EmployeeId = request.EmployeeId,

            ClientEntityId = request.ClientEntityId,

            Type = request.Type,

            DocumentNumber = request.DocumentNumber.Trim(),

            IssueDate = request.IssueDate,

            ExpiryDate = request.ExpiryDate,

            FileUrl = request.FileUrl?.Trim(),

            FileName = request.FileName?.Trim(),

            ContentType = request.ContentType?.Trim(),

            FileSize = request.FileSize,

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
