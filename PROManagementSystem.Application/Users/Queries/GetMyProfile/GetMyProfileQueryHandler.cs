using MediatR;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Users.Queries.GetMyProfile;

public sealed class GetMyProfileQueryHandler
    : IRequestHandler<GetMyProfileQuery, GetMyProfileResponseDto>
{
    private readonly IUserReadRepository _userReadRepository;
    private readonly ICurrentUserService _currentUser;

    public GetMyProfileQueryHandler(
        IUserReadRepository userReadRepository,
        ICurrentUserService currentUser)
    {
        _userReadRepository = userReadRepository;
        _currentUser = currentUser;
    }

    public async Task<GetMyProfileResponseDto> Handle(
        GetMyProfileQuery request,
        CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated ||
            !_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated.");
        }

        var user = await _userReadRepository.GetByIdAsync(
            _currentUser.UserId.Value,
            cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException(
                "User profile was not found.");
        }

        return user;
    }
}