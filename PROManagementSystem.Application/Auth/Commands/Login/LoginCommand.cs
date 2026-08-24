using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.Login;

public sealed record LoginCommand(
    string Email,
    string Password
) : IRequest<LoginResponseDto>;