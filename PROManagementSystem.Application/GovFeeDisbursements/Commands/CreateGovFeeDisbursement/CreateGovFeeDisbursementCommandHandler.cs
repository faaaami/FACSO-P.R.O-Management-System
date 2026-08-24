using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Exceptions;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.GovFeeDisbursements.Commands.CreateGovFeeDisbursement;

public sealed class CreateGovFeeDisbursementCommandHandler
    : IRequestHandler<
        CreateGovFeeDisbursementCommand,
        CreateGovFeeDisbursementResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateGovFeeDisbursementCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateGovFeeDisbursementResponseDto> Handle(
        CreateGovFeeDisbursementCommand request,
        CancellationToken cancellationToken)
    {
        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x =>
                        x.Id == request.ClientCompanyId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new NotFoundException(
                "Client company was not found.");
        }

        if (request.RenewalTaskId.HasValue)
        {
            var task =
                await _context.RenewalTasks
                    .FirstOrDefaultAsync(
                        x =>
                            x.Id ==
                                request.RenewalTaskId.Value &&
                            !x.IsDeleted,
                        cancellationToken);

            if (task is null)
            {
                throw new NotFoundException(
                    "Renewal task was not found.");
            }

            if (task.ClientCompanyId !=
                request.ClientCompanyId)
            {
                throw new ConflictException(
                    "The renewal task does not belong to the specified client company.");
            }
        }

        var now = DateTime.UtcNow;

        var disbursement = new GovFeeDisbursement
        {
            Id = Guid.NewGuid(),

            ClientCompanyId =
                request.ClientCompanyId,

            RenewalTaskId =
                request.RenewalTaskId,

            FeeDescription =
                request.FeeDescription.Trim(),

            GovernmentReference =
                request.GovernmentReference?.Trim(),

            Amount =
                request.Amount,

            Currency =
                request.Currency.Trim(),

            Status =
                Domain.Enums
                    .GovFeeDisbursementStatus.PaidByFirm,

            PaidByFirmAt =
                now,

            InvoicedAt =
                null,

            ReimbursedAt =
                null,

            Notes =
                request.Notes?.Trim(),

            CreatedAt =
                now,

            UpdatedAt =
                now,

            IsDeleted =
                false,

            DeletedAt =
                null
        };

        _context.GovFeeDisbursements.Add(disbursement);

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "GovFeeDisbursementCreated",
            EntityType = "GovFeeDisbursement",
            EntityId = disbursement.Id,
            Description =
                $"Gov fee disbursement '{disbursement.FeeDescription}' was created for {disbursement.Amount} {disbursement.Currency}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };

        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateGovFeeDisbursementResponseDto(
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
            disbursement.CreatedAt);
    }
}
