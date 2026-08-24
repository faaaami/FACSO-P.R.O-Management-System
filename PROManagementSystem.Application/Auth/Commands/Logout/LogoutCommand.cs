using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.Logout;

public sealed record LogoutCommand(
    string RefreshToken
) : IRequest;