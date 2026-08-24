using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Users.Commands.UpdateMyProfile;

public sealed class UpdateMyProfileCommandHandler
    : IRequestHandler<
        UpdateMyProfileCommand,
        UpdateMyProfileResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public UpdateMyProfileCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<UpdateMyProfileResponseDto> Handle(
        UpdateMyProfileCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Make sure the caller is authenticated
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;

        // 2. Load only the current user's record
        var user = await _context.Users
            .FirstOrDefaultAsync(
                x => x.Id == userId,
                cancellationToken);

        if (user is null)
        {
            throw new KeyNotFoundException(
                "User profile was not found.");
        }

        // 3. Update only fields allowed by this endpoint
        user.FullName = request.FullName.Trim();
        user.Phone = string.IsNullOrWhiteSpace(request.Phone)
            ? null
            : request.Phone.Trim();

        // 4. Update audit timestamp
        user.UpdatedAt = DateTime.UtcNow;

        // 5. Persist changes through EF Core
        await _context.SaveChangesAsync(
            cancellationToken);

        // 6. Return safe profile data
        return new UpdateMyProfileResponseDto(
            user.Id,
            user.FullName,
            user.Email,
            user.Phone,
            user.Role,
            user.IsActive,
            user.EmailVerified,
            user.ClientCompanyId);
    }
}