using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.ForgotPassword;

public sealed record ForgotPasswordCommand(
    string Email
) : IRequest<ForgotPasswordResponseDto>;