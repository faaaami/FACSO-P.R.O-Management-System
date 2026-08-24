using MediatR;

namespace PROManagementSystem.Application.Clients.Commands.CreateClientCompany;

public sealed record CreateClientCompanyCommand(
    string CompanyName,
    string? TradeLicenseNumber,
    string? Phone,
    string? Email,
    string? Address,
    string? Emirate
) : IRequest<CreateClientCompanyResponseDto>;