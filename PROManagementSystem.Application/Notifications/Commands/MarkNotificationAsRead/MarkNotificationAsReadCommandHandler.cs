using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Notifications.Commands.MarkNotificationAsRead;

public sealed class MarkNotificationAsReadCommandHandler
    : IRequestHandler<
        MarkNotificationAsReadCommand,
        MarkNotificationAsReadResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkNotificationAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkNotificationAsReadResponseDto> Handle(
        MarkNotificationAsReadCommand request,
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

        // 2. Find the notification belonging to the current user
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(
                x => x.Id == request.NotificationId &&
                     x.UserId == userId,
                cancellationToken);

        // 3. Don't allow access to another user's notification
        if (notification is null)
        {
            throw new KeyNotFoundException(
                "Notification was not found.");
        }

        // 4. Mark as read
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync(
                cancellationToken);
        }

        // 5. Return result
        return new MarkNotificationAsReadResponseDto(
            notification.Id,
            notification.IsRead,
            notification.ReadAt);
    }


}