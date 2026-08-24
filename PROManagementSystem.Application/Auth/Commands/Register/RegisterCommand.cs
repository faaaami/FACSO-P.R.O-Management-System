using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.Register;

public sealed record RegisterCommand(
    string FullName,
    string Email,
    string Password,
    string? Phone
) : IRequest<RegisterResponseDto>;