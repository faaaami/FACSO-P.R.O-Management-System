using MediatR;

namespace PROManagementSystem.Application.Contracts.Commands.CreateServiceContract;

public sealed record CreateServiceContractCommand(
    Guid ClientCompanyId,
    string ContractNumber,
    DateTime StartDate,
    decimal RetainerAmount,
    string? Terms
) : IRequest<CreateServiceContractResponseDto>;