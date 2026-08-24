using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed class UpdateServiceContractCommandHandler
    : IRequestHandler<
        UpdateServiceContractCommand,
        UpdateServiceContractResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public UpdateServiceContractCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<UpdateServiceContractResponseDto> Handle(
        UpdateServiceContractCommand request,
        CancellationToken cancellationToken)
    {
        if (request.RetainerAmount < 0)
        {
            throw new ArgumentException(
                "Retainer amount cannot be negative.");
        }

        var contract =
            await _context.ServiceContracts
                .FirstOrDefaultAsync(
                    x =>
                        x.Id == request.Id &&
                        !x.IsDeleted,
                    cancellationToken);

        if (contract is null)
        {
            throw new KeyNotFoundException(
                "Service contract was not found.");
        }

        if (request.EndDate.HasValue &&
            request.EndDate.Value < request.StartDate)
        {
            throw new ArgumentException(
                "End date cannot be earlier than start date.");
        }

        contract.StartDate =
            request.StartDate;

        contract.EndDate =
            request.EndDate;

        contract.RetainerAmount =
            request.RetainerAmount;

        contract.Terms =
            request.Terms?.Trim();

        contract.UpdatedAt =
            DateTime.UtcNow;

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceContractUpdated",
            EntityType = "ServiceContract",
            EntityId = contract.Id,
            Description =
                $"Service contract '{contract.ContractNumber}' was updated.",
            CreatedAt = contract.UpdatedAt,
            UpdatedAt = contract.UpdatedAt,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new UpdateServiceContractResponseDto(
            contract.Id,
            contract.ClientCompanyId,
            contract.ContractNumber,
            contract.StartDate,
            contract.EndDate,
            contract.Status,
            contract.RetainerAmount,
            contract.Terms,
            contract.UpdatedAt);
    }
}