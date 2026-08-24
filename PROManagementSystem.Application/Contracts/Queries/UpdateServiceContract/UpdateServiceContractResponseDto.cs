using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed record UpdateServiceContractResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ContractNumber,
    DateTime StartDate,
    DateTime? EndDate,
    ServiceContractStatus Status,
    decimal RetainerAmount,
    string? Terms,
    DateTime UpdatedAt
);