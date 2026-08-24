using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ServiceRequestReadRepository
    : IServiceRequestReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ServiceRequestReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetServiceRequestsResponseDto>
        GetServiceRequestsAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        const string countSql = """
            SELECT COUNT(*)
            FROM service_requests
            WHERE "IsDeleted" = false;
            """;

        const string dataSql = """
            SELECT
                "Id" AS Id,
                "ClientCompanyId" AS ClientCompanyId,
                "DocumentId" AS DocumentId,
                "Status" AS Status,
                "Description" AS Description,
                "RejectionReason" AS RejectionReason,
                "ConvertedAt" AS ConvertedAt,
                "RejectedAt" AS RejectedAt,
                "ConvertedRenewalTaskId" AS ConvertedRenewalTaskId,
                "CreatedAt" AS CreatedAt
            FROM service_requests
            WHERE "IsDeleted" = false
            ORDER BY "CreatedAt" DESC
            OFFSET @Offset
            LIMIT @PageSize;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
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
            await connection.QueryAsync<
                ServiceRequestListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetServiceRequestsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetServiceRequestResponseDto?>
        GetServiceRequestByIdAsync(
            Guid id,
            CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                "Id" AS Id,
                "ClientCompanyId" AS ClientCompanyId,
                "DocumentId" AS DocumentId,
                "Status" AS Status,
                "Description" AS Description,
                "RejectionReason" AS RejectionReason,
                "ConvertedAt" AS ConvertedAt,
                "RejectedAt" AS RejectedAt,
                "ConvertedRenewalTaskId" AS ConvertedRenewalTaskId,
                "CreatedAt" AS CreatedAt,
                "UpdatedAt" AS UpdatedAt
            FROM service_requests
            WHERE "Id" = @Id
              AND "IsDeleted" = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetServiceRequestResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id },
                cancellationToken: cancellationToken));
    }
}