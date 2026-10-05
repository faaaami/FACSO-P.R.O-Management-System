using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications.Queries.GetNotifications;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class NotificationReadRepository
    : INotificationReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public NotificationReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<(
        IReadOnlyList<NotificationDto> Items,
        int TotalCount)>
        GetByUserIdAsync(
            Guid userId,
            int page,
            int pageSize,
            string sortBy,
            string sortDir,
            CancellationToken cancellationToken = default)
    {
        var offset = (page - 1) * pageSize;

        var orderBy = sortBy.ToLowerInvariant() switch
        {
            "title" => SqlColumnNames.NotificationTitle,
            "isread" => SqlColumnNames.NotificationIsRead,
            _ => SqlColumnNames.NotificationCreatedAt
        };

        var direction =
            sortDir.Equals(
                "asc",
                StringComparison.OrdinalIgnoreCase)
                ? "ASC"
                : "DESC";

        var sql = $"""
            SELECT
                {SqlColumnNames.NotificationId} AS Id,
                {SqlColumnNames.NotificationTitle} AS Title,
                {SqlColumnNames.NotificationMessage} AS Message,
                {SqlColumnNames.NotificationIsRead} AS IsRead,
                {SqlColumnNames.NotificationReadAt} AS ReadAt,
                {SqlColumnNames.NotificationCreatedAt} AS CreatedAt
            FROM notifications
            WHERE {SqlColumnNames.NotificationUserId} = @UserId
              AND {SqlColumnNames.NotificationIsDeleted} = false
            ORDER BY {orderBy} {direction}
            {SqlSnippets.Pagination};

            SELECT COUNT(*)
            FROM notifications
            WHERE {SqlColumnNames.NotificationUserId} = @UserId
              AND {SqlColumnNames.NotificationIsDeleted} = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        using var multi =
            await connection.QueryMultipleAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        UserId = userId,
                        PageSize = pageSize,
                        Offset = offset
                    },
                    cancellationToken:
                        cancellationToken));

        var items =
            (await multi.ReadAsync<NotificationDto>())
            .AsList();

        var totalCount =
            await multi.ReadSingleAsync<int>();

        return (items, totalCount);
    }

    public async Task<int> GetUnreadCountAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var sql = $"""
            SELECT COUNT(*)::int
            FROM notifications
            WHERE {SqlColumnNames.NotificationUserId} = @UserId
              AND {SqlColumnNames.NotificationIsRead} = false
              AND {SqlColumnNames.NotificationIsDeleted} = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                sql,
                new { UserId = userId },
                cancellationToken: cancellationToken));
    }
}