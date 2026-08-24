using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientEntity;

public sealed record UpdateClientEntityCommand(
    Guid ClientCompanyId,
    Guid EntityId,
    string EntityName,
    string? TradeLicenseNumber,
    string? Emirate,
    bool IsActive
) : IRequest<UpdateClientEntityResponseDto>;