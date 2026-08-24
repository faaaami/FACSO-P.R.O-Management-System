using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Tasks.Commands.CreateRenewalTask;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.ConvertServiceRequest;

public sealed class ConvertServiceRequestCommandHandler
    : IRequestHandler<
        ConvertServiceRequestCommand,
        ConvertServiceRequestResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly ICurrentUserService _currentUser;

    public ConvertServiceRequestCommandHandler(
        IApplicationDbContext context,
        ISender sender,
        ICurrentUserService currentUser)
    {
        _context = context;
        _sender = sender;
        _currentUser = currentUser;
    }

    public async Task<ConvertServiceRequestResponseDto> Handle(
        ConvertServiceRequestCommand request,
        CancellationToken cancellationToken)
    {
        var serviceRequest =
            await _context.ServiceRequests
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.ServiceRequestId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (serviceRequest is null)
        {
            throw new NotFoundException(
                "Service request was not found.");
        }

        if (serviceRequest.Status !=
            ServiceRequestStatus.Submitted)
        {
            throw new ConflictException(
                "Only submitted service requests can be converted.");
        }

        // Reuse the existing Day 4 renewal-task creation flow.
        var renewalTask =
            await _sender.Send(
                new CreateRenewalTaskCommand(
                    serviceRequest.DocumentId),
                cancellationToken);

        var now = DateTime.UtcNow;

        serviceRequest.Status =
            ServiceRequestStatus.Converted;

        serviceRequest.ConvertedAt =
            now;

        serviceRequest.ConvertedRenewalTaskId =
            renewalTask.Id;

        serviceRequest.UpdatedAt =
            now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceRequestConverted",
            EntityType = "ServiceRequest",
            EntityId = serviceRequest.Id,
            Description =
                $"Service request was converted to renewal task {renewalTask.Id}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new ConvertServiceRequestResponseDto(
            serviceRequest.Id,
            renewalTask.Id,
            serviceRequest.Status,
            serviceRequest.ConvertedAt.Value);
    }
}