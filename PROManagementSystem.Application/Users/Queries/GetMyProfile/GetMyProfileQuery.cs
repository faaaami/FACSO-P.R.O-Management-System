using MediatR;

namespace PROManagementSystem.Application.Users.Queries.GetMyProfile;

public sealed record GetMyProfileQuery
    : IRequest<GetMyProfileResponseDto>;