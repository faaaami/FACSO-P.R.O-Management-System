using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Queries.GetClientContracts;

public sealed record GetClientContractsResponseDto(
    List<ServiceContractListItemDto> Items
);

public sealed record ServiceContractListItemDto(
    Guid Id,
    Guid ClientCompanyId,
    string ContractNumber,
    DateTime StartDate,
    DateTime? EndDate,
    ServiceContractStatus Status,
    decimal RetainerAmount,
    string? Terms
);