using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Notifications.Queries.GetNotifications;

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
            "title" => "title",
            "isread" => "\"IsRead\"",
            _ => "\"CreatedAt\""
        };

        var direction =
            sortDir.Equals(
                "asc",
                StringComparison.OrdinalIgnoreCase)
                ? "ASC"
                : "DESC";

        var sql = $"""
            SELECT
                "Id" AS Id,
                "Title" AS Title,
                "Message" AS Message,
                "IsRead" AS IsRead,
                "ReadAt" AS ReadAt,
                "CreatedAt" AS CreatedAt
            FROM notifications
            WHERE "UserId" = @UserId
              AND "IsDeleted" = false
            ORDER BY {orderBy} {direction}
            LIMIT @PageSize
            OFFSET @Offset;

            SELECT COUNT(*)
            FROM notifications
            WHERE "UserId" = @UserId
              AND "IsDeleted" = false;
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
}