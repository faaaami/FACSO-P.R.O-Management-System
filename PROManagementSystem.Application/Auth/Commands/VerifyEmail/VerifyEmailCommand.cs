using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.VerifyEmail;

public sealed record VerifyEmailCommand(
    string Token
) : IRequest<VerifyEmailResponseDto>;