using Dapper;
using Npgsql;
using PROManagementSystem.Application.AuditLogs.Queries.GetAuditLog;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class AuditLogReadRepository
    : IAuditLogReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public AuditLogReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetAuditLogResponseDto> GetListAsync(
        string? entityType,
        Guid? entityId,
        Guid? userId,
        DateTime? from,
        DateTime? to,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var conditions = new List<string>
        {
            "al.is_deleted = false"
        };

        if (!string.IsNullOrEmpty(entityType))
        {
            conditions.Add("al.entity_type = @EntityType");
            parameters.Add("EntityType", entityType);
        }

        if (entityId.HasValue)
        {
            conditions.Add("al.entity_id = @EntityId");
            parameters.Add("EntityId", entityId.Value);
        }

        if (userId.HasValue)
        {
            conditions.Add("al.user_id = @UserId");
            parameters.Add("UserId", userId.Value);
        }

        if (from.HasValue)
        {
            conditions.Add("al.created_at >= @From");
            parameters.Add("From", from.Value);
        }

        if (to.HasValue)
        {
            conditions.Add("al.created_at <= @To");
            parameters.Add("To", to.Value);
        }

        var whereClause =
            string.Join(" AND ", conditions);

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var countSql =
            $"""
            SELECT COUNT(*)
            FROM audit_logs al
            WHERE {whereClause}
            """;

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var offset = (page - 1) * pageSize;
        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var dataSql =
            $"""
            SELECT
                al.id AS "Id",
                al.user_id AS "UserId",
                u.full_name AS "UserName",
                al.action AS "Action",
                al.entity_type AS "EntityType",
                al.entity_id AS "EntityId",
                al.employee_id AS "EmployeeId",
                al.description AS "Description",
                al.metadata AS "Metadata",
                al.created_at AS "CreatedAt"
            FROM audit_logs al
            LEFT JOIN users u ON u.id = al.user_id
            WHERE {whereClause}
            ORDER BY al.created_at DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;
            """;

        var items =
            (await connection.QueryAsync<AuditLogItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken)))
            .ToList();

        return new GetAuditLogResponseDto(
            items,
            totalCount,
            page,
            pageSize);
    }
}
