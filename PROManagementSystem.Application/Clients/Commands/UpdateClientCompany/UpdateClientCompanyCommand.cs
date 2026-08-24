using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.UpdateClientCompany;

public sealed record UpdateClientCompanyCommand(
    Guid Id,
    string? CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Emirate,
    bool? IsActive
) : IRequest<UpdateClientCompanyResponseDto>;