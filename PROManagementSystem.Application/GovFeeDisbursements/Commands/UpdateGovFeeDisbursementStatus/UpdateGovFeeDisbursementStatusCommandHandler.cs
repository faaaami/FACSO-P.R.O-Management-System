using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.UpdateGovFeeDisbursementStatus;

public sealed class UpdateGovFeeDisbursementStatusCommandHandler
    : IRequestHandler<
        UpdateGovFeeDisbursementStatusCommand,
        UpdateGovFeeDisbursementStatusResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateGovFeeDisbursementStatusCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateGovFeeDisbursementStatusResponseDto> Handle(
        UpdateGovFeeDisbursementStatusCommand request,
        CancellationToken cancellationToken)
    {
        var disbursement =
            await _context.GovFeeDisbursements
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (disbursement is null)
        {
            throw new NotFoundException(
                "Gov fee disbursement was not found.");
        }

        var now = DateTime.UtcNow;

        switch (disbursement.Status)
        {
            case GovFeeDisbursementStatus.PaidByFirm:
                if (request.Status !=
                    GovFeeDisbursementStatus.InvoicedToClient)
                {
                    throw new ConflictException(
                        $"Cannot transition from {disbursement.Status} to {request.Status}. Only transition to InvoicedToClient is allowed.");
                }

                disbursement.InvoicedAt = now;
                break;

            case GovFeeDisbursementStatus.InvoicedToClient:
                if (request.Status !=
                    GovFeeDisbursementStatus.Reimbursed)
                {
                    throw new ConflictException(
                        $"Cannot transition from {disbursement.Status} to {request.Status}. Only transition to Reimbursed is allowed.");
                }

                disbursement.ReimbursedAt = now;
                break;

            case GovFeeDisbursementStatus.Reimbursed:
                throw new ConflictException(
                    "This disbursement has already been reimbursed. No further transitions are allowed.");

            default:
                throw new ConflictException(
                    $"Cannot transition from {disbursement.Status} to {request.Status}.");
        }

        disbursement.Status = request.Status;
        disbursement.UpdatedAt = now;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "GovFeeDisbursementStatusUpdated",
            EntityType = "GovFeeDisbursement",
            EntityId = disbursement.Id,
            Description =
                $"Gov fee disbursement status changed to {request.Status}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateGovFeeDisbursementStatusResponseDto(
            disbursement.Id,
            disbursement.ClientCompanyId,
            disbursement.RenewalTaskId,
            disbursement.FeeDescription,
            disbursement.GovernmentReference,
            disbursement.Amount,
            disbursement.Currency,
            disbursement.Status,
            disbursement.PaidByFirmAt,
            disbursement.InvoicedAt,
            disbursement.ReimbursedAt,
            disbursement.Notes,
            disbursement.CreatedAt,
            disbursement.UpdatedAt);
    }
}
