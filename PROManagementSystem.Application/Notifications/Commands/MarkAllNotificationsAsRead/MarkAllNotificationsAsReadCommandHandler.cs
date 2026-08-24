using MediatR;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Notifications.Commands.MarkAllNotificationsAsRead;

public sealed class MarkAllNotificationsAsReadCommandHandler
    : IRequestHandler<
        MarkAllNotificationsAsReadCommand,
        MarkAllNotificationsAsReadResponseDto>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public MarkAllNotificationsAsReadCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<MarkAllNotificationsAsReadResponseDto> Handle(
        MarkAllNotificationsAsReadCommand request,
        CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated ||
            !_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException(
                "User is not authenticated.");
        }

        var userId = _currentUserService.UserId.Value;

        var notifications = await _context.Notifications
            .Where(x =>
                x.UserId == userId &&
                !x.IsRead)
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return new MarkAllNotificationsAsReadResponseDto(0);
        }

        var readAt = DateTime.UtcNow;

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = readAt;
            notification.UpdatedAt = readAt;
        }

        await _context.SaveChangesAsync(
            cancellationToken);

        return new MarkAllNotificationsAsReadResponseDto(
            notifications.Count);
    }
}