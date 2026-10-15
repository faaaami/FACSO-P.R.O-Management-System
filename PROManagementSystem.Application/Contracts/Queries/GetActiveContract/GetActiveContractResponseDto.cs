using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Queries.GetActiveContract;

public sealed record GetActiveContractResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ContractNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    ServiceContractStatus Status,
    decimal RetainerAmount,
    string? Terms
);