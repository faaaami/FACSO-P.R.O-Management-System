using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientEntity;

public sealed record CreateClientEntityCommand(
    Guid ClientCompanyId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate
) : IRequest<CreateClientEntityResponseDto>;