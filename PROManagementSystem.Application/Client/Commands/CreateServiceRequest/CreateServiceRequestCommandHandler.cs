using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DomainEvents;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Client.Commands.CreateServiceRequest;

public sealed class CreateServiceRequestCommandHandler
    : IRequestHandler<
        CreateServiceRequestCommand,
        CreateServiceRequestResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IPublisher _publisher;

    public CreateServiceRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser,
        IPublisher publisher)
    {
        _context = context;
        _currentUser = currentUser;
        _publisher = publisher;
    }

    public async Task<CreateServiceRequestResponseDto> Handle(
        CreateServiceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var clientCompanyId = _currentUser.ClientCompanyId;

        if (!clientCompanyId.HasValue)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x => x.Id == clientCompanyId.Value &&
                         !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        // ============================================================
        // Resolve and validate the subject
        // ============================================================

        Guid? employeeId = null;
        Guid? entityId = null;

        if (request.EmployeeId.HasValue)
        {
            var employee =
                await _context.Employees
                    .Include(x => x.ClientEntity)
                    .FirstOrDefaultAsync(
                        x => x.Id == request.EmployeeId.Value &&
                             !x.IsDeleted,
                        cancellationToken);

            if (employee is null ||
                employee.ClientEntity is null ||
                employee.ClientEntity.ClientCompanyId !=
                    clientCompanyId.Value)
            {
                throw new KeyNotFoundException(
                    "Employee was not found.");
            }

            employeeId = employee.Id;
            entityId = employee.ClientEntityId;
        }
        else if (request.EntityId.HasValue)
        {
            var entity =
                await _context.ClientEntities
                    .FirstOrDefaultAsync(
                        x => x.Id == request.EntityId.Value &&
                             !x.IsDeleted,
                        cancellationToken);

            if (entity is null ||
                entity.ClientCompanyId != clientCompanyId.Value)
            {
                throw new KeyNotFoundException(
                    "Client entity was not found.");
            }

            entityId = entity.Id;
        }

        // Both provided: the employee must belong to the entity.
        if (request.EmployeeId.HasValue &&
            request.EntityId.HasValue &&
            request.EntityId.Value != entityId)
        {
            throw new ArgumentException(
                "Employee does not belong to the specified entity.");
        }

        // ============================================================
        // Resolve the document for early renewals
        // ============================================================

        Guid? documentId = null;

        if (request.Type == ServiceRequestType.EarlyRenewal)
        {
            var query = _context.Documents
                .AsNoTracking()
                .Where(
                    x =>
                        !x.IsDeleted &&
                        x.IsActive &&
                        x.Type == DocumentType.Visa);

            if (employeeId.HasValue)
            {
                var targetEmployeeId = employeeId.Value;

                query = query
                    .Where(x => x.EmployeeId == targetEmployeeId);
            }
            else
            {
                var targetEntityId = entityId!.Value;

                query = query
                    .Where(x => x.ClientEntityId == targetEntityId);
            }

            var visaDocument =
                await query
                    .OrderByDescending(x => x.ExpiryDate)
                    .FirstOrDefaultAsync(
                        cancellationToken);

            if (visaDocument is null)
            {
                throw new ConflictException(
                    "No active visa document found for early renewal.");
            }

            documentId = visaDocument.Id;
        }

        // ============================================================
        // Duplicate guard: one open request per type + subject + company
        // ============================================================

        var duplicateExists =
            await _context.ServiceRequests
                .AnyAsync(
                    x =>
                        x.ClientCompanyId == clientCompanyId.Value &&
                        x.Type == request.Type &&
                        x.Status == ServiceRequestStatus.Submitted &&
                        !x.IsDeleted &&
                        (employeeId.HasValue
                            ? x.EmployeeId == employeeId.Value
                            : x.EntityId == entityId!.Value),
                    cancellationToken);

        if (duplicateExists)
        {
            throw new ConflictException(
                "An open service request with the same type and subject already exists.");
        }

        // ============================================================
        // Create the request
        // ============================================================

        var now = DateTime.UtcNow;

        var serviceRequest = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            ClientCompanyId = clientCompanyId.Value,
            Type = request.Type,
            EmployeeId = employeeId,
            EntityId = entityId,
            DocumentId = documentId,
            Status = ServiceRequestStatus.Submitted,
            Description = request.Notes,
            RejectionReason = null,
            ConvertedAt = null,
            RejectedAt = null,
            ConvertedRenewalTaskId = null,
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.ServiceRequests.Add(serviceRequest);

        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceRequestCreated",
            EntityType = "ServiceRequest",
            EntityId = serviceRequest.Id,
            Description =
                $"Service request of type {request.Type} was created.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        // ============================================================
        // Publish the event after the save succeeds
        // ============================================================

        await _publisher.Publish(
            new ServiceRequestSubmittedEvent(
                serviceRequest.Id,
                serviceRequest.ClientCompanyId,
                serviceRequest.Type,
                serviceRequest.EmployeeId,
                serviceRequest.EntityId,
                serviceRequest.DocumentId,
                serviceRequest.Description),
            cancellationToken);

        return new CreateServiceRequestResponseDto(
            serviceRequest.Id,
            serviceRequest.ClientCompanyId,
            serviceRequest.Type,
            serviceRequest.EmployeeId,
            serviceRequest.EntityId,
            serviceRequest.DocumentId,
            serviceRequest.Status,
            serviceRequest.Description,
            serviceRequest.CreatedAt);
    }
}