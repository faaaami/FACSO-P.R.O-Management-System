using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;

public sealed record CreateServiceContractResponseDto(
    Guid Id,
    Guid ClientCompanyId,
    string ContractNumber,
    DateOnly StartDate,
    DateOnly? EndDate,
    ServiceContractStatus Status,
    decimal RetainerAmount,
    string? Terms
);