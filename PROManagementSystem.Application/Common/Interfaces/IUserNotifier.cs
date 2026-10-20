namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>Writes in-app notifications and pushes them in real time (best effort).</summary>
public interface IUserNotifier
{
    Task NotifyUsersAsync(
        IReadOnlyCollection<Guid> userIds,
        string title,
        string message,
        CancellationToken cancellationToken);

    /// <summary>Notifies every active, approved contact of a client company.</summary>
    Task NotifyClientContactsAsync(
        Guid clientCompanyId,
        string title,
        string message,
        CancellationToken cancellationToken);
}
