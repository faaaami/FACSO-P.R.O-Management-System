using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Queries.GetServiceContract;

public sealed record GetServiceContractResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ContractNumber,
    DateTime StartDate,
    DateTime? EndDate,
    ServiceContractStatus Status,
    decimal RetainerAmount,
    string? Terms,
    DateTime CreatedAt,
    DateTime UpdatedAt
);