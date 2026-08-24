using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.RefreshToken;

public sealed record RefreshTokenCommand(
    string RefreshToken
) : IRequest<RefreshTokenResponseDto>;