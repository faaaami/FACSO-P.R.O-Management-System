using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.ServiceRequests.Commands.RejectServiceRequest;

public sealed class RejectServiceRequestCommandHandler
    : IRequestHandler<
        RejectServiceRequestCommand,
        RejectServiceRequestResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public RejectServiceRequestCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<RejectServiceRequestResponseDto> Handle(
        RejectServiceRequestCommand request,
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
                "Only submitted service requests can be rejected.");
        }

        var reason = request.Reason.Trim();
        var now = DateTime.UtcNow;

        serviceRequest.Status =
            ServiceRequestStatus.Rejected;

        serviceRequest.RejectionReason =
            reason;

        serviceRequest.RejectedAt =
            now;

        serviceRequest.UpdatedAt =
            now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceRequestRejected",
            EntityType = "ServiceRequest",
            EntityId = serviceRequest.Id,
            Description =
                $"Service request was rejected: {reason}",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new RejectServiceRequestResponseDto(
            serviceRequest.Id,
            serviceRequest.Status,
            serviceRequest.RejectionReason,
            serviceRequest.RejectedAt.Value);
    }
}