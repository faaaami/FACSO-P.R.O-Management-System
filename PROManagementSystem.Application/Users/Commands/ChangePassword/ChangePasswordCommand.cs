using MediatR;

namespace PROManagementSystem.Application.Users.Commands.ChangePassword;

public sealed record ChangePasswordCommand(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
) : IRequest;
