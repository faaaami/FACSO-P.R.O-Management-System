using PROManagementSystem.Application.Users.Queries.GetMyProfile;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IUserReadRepository
{
    Task<GetMyProfileResponseDto?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}