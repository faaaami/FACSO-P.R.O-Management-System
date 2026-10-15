using MediatR;

namespace PROManagementSystem.Application.Contracts.Commands.UpdateServiceContract;

public sealed record UpdateServiceContractCommand(
    Guid Id,
    DateOnly StartDate,
    DateOnly? EndDate,
    decimal RetainerAmount,
    string? Terms
) : IRequest<UpdateServiceContractResponseDto>;