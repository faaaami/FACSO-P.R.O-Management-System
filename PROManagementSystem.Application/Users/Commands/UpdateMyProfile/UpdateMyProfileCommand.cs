using MediatR;

namespace PROManagementSystem.Application.Users.Commands.UpdateMyProfile;

public sealed record UpdateMyProfileCommand(
    string FullName,
    string? Phone
) : IRequest<UpdateMyProfileResponseDto>;