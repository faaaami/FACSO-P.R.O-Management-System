using MediatR;

namespace PROManagementSystem.Application.Auth.Commands.ResetPassword;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword
) : IRequest<ResetPasswordResponseDto>;