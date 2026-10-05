using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequest;
using PROManagementSystem.Application.ServiceRequests.Queries.GetServiceRequests;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

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

        var countSql = $"""
            SELECT COUNT(*)
            FROM service_requests
            WHERE {SqlColumnNames.ServiceRequestIsDeleted} = false;
            """;

        var dataSql = $"""
            SELECT
                {SqlColumnNames.ServiceRequestId} AS Id,
                {SqlColumnNames.ServiceRequestClientCompanyId} AS ClientCompanyId,
                {SqlColumnNames.ServiceRequestType} AS Type,
                {SqlColumnNames.ServiceRequestEmployeeId} AS EmployeeId,
                {SqlColumnNames.ServiceRequestEntityId} AS EntityId,
                {SqlColumnNames.ServiceRequestDocumentId} AS DocumentId,
                {SqlColumnNames.ServiceRequestStatus} AS Status,
                {SqlColumnNames.ServiceRequestDescription} AS Description,
                {SqlColumnNames.ServiceRequestRejectionReason} AS RejectionReason,
                {SqlColumnNames.ServiceRequestConvertedAt} AS ConvertedAt,
                {SqlColumnNames.ServiceRequestRejectedAt} AS RejectedAt,
                {SqlColumnNames.ServiceRequestConvertedRenewalTaskId} AS ConvertedRenewalTaskId,
                {SqlColumnNames.ServiceRequestCreatedAt} AS CreatedAt
            FROM service_requests
            WHERE {SqlColumnNames.ServiceRequestIsDeleted} = false
            ORDER BY {SqlColumnNames.ServiceRequestCreatedAt} DESC
            {SqlSnippets.Pagination};
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
        var sql = $"""
            SELECT
                {SqlColumnNames.ServiceRequestId} AS Id,
                {SqlColumnNames.ServiceRequestClientCompanyId} AS ClientCompanyId,
                {SqlColumnNames.ServiceRequestType} AS Type,
                {SqlColumnNames.ServiceRequestEmployeeId} AS EmployeeId,
                {SqlColumnNames.ServiceRequestEntityId} AS EntityId,
                {SqlColumnNames.ServiceRequestDocumentId} AS DocumentId,
                {SqlColumnNames.ServiceRequestStatus} AS Status,
                {SqlColumnNames.ServiceRequestDescription} AS Description,
                {SqlColumnNames.ServiceRequestRejectionReason} AS RejectionReason,
                {SqlColumnNames.ServiceRequestConvertedAt} AS ConvertedAt,
                {SqlColumnNames.ServiceRequestRejectedAt} AS RejectedAt,
                {SqlColumnNames.ServiceRequestConvertedRenewalTaskId} AS ConvertedRenewalTaskId,
                {SqlColumnNames.ServiceRequestCreatedAt} AS CreatedAt,
                {SqlColumnNames.ServiceRequestUpdatedAt} AS UpdatedAt
            FROM service_requests
            WHERE {SqlColumnNames.ServiceRequestId} = @Id
              AND {SqlColumnNames.ServiceRequestIsDeleted} = false;
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

    public async Task<GetServiceRequestsResponseDto>
        GetServiceRequestsByCompanyAsync(
            Guid clientCompanyId,
            ServiceRequestStatus? status,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereClause =
            $"{SqlColumnNames.ServiceRequestClientCompanyId} = @ClientCompanyId " +
            $"AND {SqlColumnNames.ServiceRequestIsDeleted} = false";

        var parameters = new
        {
            ClientCompanyId = clientCompanyId,
            Status = (int?)status,
            Offset = offset,
            PageSize = pageSize
        };

        var countSql = $"""
            SELECT COUNT(*)
            FROM service_requests
            WHERE {whereClause}
              AND (@Status IS NULL OR {SqlColumnNames.ServiceRequestStatus} = @Status);
            """;

        var dataSql = $"""
            SELECT
                {SqlColumnNames.ServiceRequestId} AS Id,
                {SqlColumnNames.ServiceRequestClientCompanyId} AS ClientCompanyId,
                {SqlColumnNames.ServiceRequestType} AS Type,
                {SqlColumnNames.ServiceRequestEmployeeId} AS EmployeeId,
                {SqlColumnNames.ServiceRequestEntityId} AS EntityId,
                {SqlColumnNames.ServiceRequestDocumentId} AS DocumentId,
                {SqlColumnNames.ServiceRequestStatus} AS Status,
                {SqlColumnNames.ServiceRequestDescription} AS Description,
                {SqlColumnNames.ServiceRequestRejectionReason} AS RejectionReason,
                {SqlColumnNames.ServiceRequestConvertedAt} AS ConvertedAt,
                {SqlColumnNames.ServiceRequestRejectedAt} AS RejectedAt,
                {SqlColumnNames.ServiceRequestConvertedRenewalTaskId} AS ConvertedRenewalTaskId,
                {SqlColumnNames.ServiceRequestCreatedAt} AS CreatedAt
            FROM service_requests
            WHERE {whereClause}
              AND (@Status IS NULL OR {SqlColumnNames.ServiceRequestStatus} = @Status)
            ORDER BY {SqlColumnNames.ServiceRequestCreatedAt} DESC
            {SqlSnippets.Pagination};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

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
        GetServiceRequestByIdForCompanyAsync(
            Guid id,
            Guid clientCompanyId,
            CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                {SqlColumnNames.ServiceRequestId} AS Id,
                {SqlColumnNames.ServiceRequestClientCompanyId} AS ClientCompanyId,
                {SqlColumnNames.ServiceRequestType} AS Type,
                {SqlColumnNames.ServiceRequestEmployeeId} AS EmployeeId,
                {SqlColumnNames.ServiceRequestEntityId} AS EntityId,
                {SqlColumnNames.ServiceRequestDocumentId} AS DocumentId,
                {SqlColumnNames.ServiceRequestStatus} AS Status,
                {SqlColumnNames.ServiceRequestDescription} AS Description,
                {SqlColumnNames.ServiceRequestRejectionReason} AS RejectionReason,
                {SqlColumnNames.ServiceRequestConvertedAt} AS ConvertedAt,
                {SqlColumnNames.ServiceRequestRejectedAt} AS RejectedAt,
                {SqlColumnNames.ServiceRequestConvertedRenewalTaskId} AS ConvertedRenewalTaskId,
                {SqlColumnNames.ServiceRequestCreatedAt} AS CreatedAt,
                {SqlColumnNames.ServiceRequestUpdatedAt} AS UpdatedAt
            FROM service_requests
            WHERE {SqlColumnNames.ServiceRequestId} = @Id
              AND {SqlColumnNames.ServiceRequestClientCompanyId} = @ClientCompanyId
              AND {SqlColumnNames.ServiceRequestIsDeleted} = false;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<
            GetServiceRequestResponseDto>(
            new CommandDefinition(
                sql,
                new { Id = id, ClientCompanyId = clientCompanyId },
                cancellationToken: cancellationToken));
    }
}