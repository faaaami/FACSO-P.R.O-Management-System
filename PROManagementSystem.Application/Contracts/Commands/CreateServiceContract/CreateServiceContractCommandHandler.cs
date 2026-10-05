using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;

public sealed class CreateServiceContractCommandHandler
    : IRequestHandler<
        CreateServiceContractCommand,
        CreateServiceContractResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;

    public CreateServiceContractCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUser)
    {
        _context = context;
        _currentUser = currentUser;
    }

    public async Task<CreateServiceContractResponseDto> Handle(
        CreateServiceContractCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ContractNumber))
        {
            throw new ArgumentException(
                "Contract number is required.");
        }

        if (request.RetainerAmount < 0)
        {
            throw new ArgumentException(
                "Retainer amount cannot be negative.");
        }

        var companyExists =
            await _context.ClientCompanies
                .AnyAsync(
                    x =>
                        x.Id == request.ClientCompanyId &&
                        !x.IsDeleted,
                    cancellationToken);

        if (!companyExists)
        {
            throw new KeyNotFoundException(
                "Client company was not found.");
        }

        var contractNumberExists =
            await _context.ServiceContracts
                .AnyAsync(
                    x =>
                        x.ContractNumber ==
                            request.ContractNumber.Trim() &&
                        !x.IsDeleted,
                    cancellationToken);

        if (contractNumberExists)
        {
            throw new InvalidOperationException(
                "A contract with this contract number already exists.");
        }

        var now = DateTime.UtcNow;

        // End the currently active contract.
        var activeContract =
            await _context.ServiceContracts
                .FirstOrDefaultAsync(
                    x =>
                        x.ClientCompanyId ==
                            request.ClientCompanyId &&
                        x.Status ==
                            ServiceContractStatus.Active &&
                        !x.IsDeleted,
                    cancellationToken);

        if (activeContract is not null)
        {
            activeContract.Status =
                ServiceContractStatus.Ended;

            activeContract.EndDate =
                now;

            activeContract.UpdatedAt =
                now;
        }

        var contract = new ServiceContract
        {
            Id = Guid.NewGuid(),

            ClientCompanyId =
                request.ClientCompanyId,

            ContractNumber =
                request.ContractNumber.Trim(),

            StartDate =
                request.StartDate,

            EndDate =
                null,

            Status =
                ServiceContractStatus.Active,

            RetainerAmount =
                request.RetainerAmount,

            Terms =
                request.Terms?.Trim(),

            CreatedAt =
                now,

            UpdatedAt =
                now,

            IsDeleted =
                false,

            DeletedAt =
                null
        };

        _context.ServiceContracts.Add(contract);

        if (activeContract is not null)
        {
            var endedAudit =
                new Domain.Entities.AuditLog
                {
                    Id = Guid.NewGuid(),
                    UserId = _currentUser.UserId,
                    Action = "ServiceContractEnded",
                    EntityType = "ServiceContract",
                    EntityId = activeContract.Id,
                    Description =
                        $"Service contract '{activeContract.ContractNumber}' was implicitly ended " +
                        $"when contract '{contract.ContractNumber}' was created " +
                        $"for company {contract.ClientCompanyId}.",
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false,
                    DeletedAt = null
                };
            _context.AuditLogs.Add(endedAudit);
        }

        var auditLog = new Domain.Entities.AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = _currentUser.UserId,
            Action = "ServiceContractCreated",
            EntityType = "ServiceContract",
            EntityId = contract.Id,
            Description =
                $"Service contract '{contract.ContractNumber}' was created for company {contract.ClientCompanyId}.",
            CreatedAt = now,
            UpdatedAt = now,
            IsDeleted = false,
            DeletedAt = null
        };
        _context.AuditLogs.Add(auditLog);

        await _context.SaveChangesAsync(
            cancellationToken);

        return new CreateServiceContractResponseDto(
            contract.Id,
            contract.ClientCompanyId,
            contract.ContractNumber,
            contract.StartDate,
            contract.EndDate,
            contract.Status,
            contract.RetainerAmount,
            contract.Terms);
    }
}