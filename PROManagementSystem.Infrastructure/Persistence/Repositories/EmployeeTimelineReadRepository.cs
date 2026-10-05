using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Employees.Queries.GetEmployeeTimeline;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class EmployeeTimelineReadRepository
    : IEmployeeTimelineReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public EmployeeTimelineReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetEmployeeTimelineResponseDto> GetTimelineAsync(
        Guid employeeId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        const string countSql = """
            SELECT COUNT(*)
            FROM audit_logs
            WHERE employee_id = @EmployeeId
              AND is_deleted = false;
            """;

        string dataSql = $"""
            SELECT
                id AS Id,
                action AS Action,
                entity_type AS EntityType,
                entity_id AS EntityId,
                description AS Description,
                created_at AS CreatedAt
            FROM audit_logs
            WHERE employee_id = @EmployeeId
              AND is_deleted = false
            ORDER BY created_at DESC
            {SqlSnippets.Pagination};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            EmployeeId = employeeId,
            Offset = offset,
            PageSize = pageSize
        };

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<EmployeeTimelineItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetEmployeeTimelineResponseDto(
            employeeId,
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }
}
