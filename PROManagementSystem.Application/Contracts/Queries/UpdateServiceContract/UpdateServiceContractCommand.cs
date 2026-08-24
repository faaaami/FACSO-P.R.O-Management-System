using MediatR;

namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed record UpdateServiceContractCommand(
    Guid Id,
    DateTime StartDate,
    DateTime? EndDate,
    decimal RetainerAmount,
    string? Terms
) : IRequest<UpdateServiceContractResponseDto>;