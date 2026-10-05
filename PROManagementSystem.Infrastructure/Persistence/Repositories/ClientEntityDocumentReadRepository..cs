using Dapper;
using Npgsql;
using PROManagementSystem.Application.Clients.Queries.GetClientEntityDocuments;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class ClientEntityDocumentReadRepository
    : IClientEntityDocumentReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public ClientEntityDocumentReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetClientEntityDocumentsResponseDto>
        GetByClientEntityIdAsync(
            Guid clientEntityId,
            bool includeDeleted,
            CancellationToken cancellationToken)
    {
        var whereConditions = new List<string>
        {
            "client_entity_id = @ClientEntityId"
        };

        if (!includeDeleted)
        {
            whereConditions.Add("is_deleted = false");
        }

        var whereClause =
            "WHERE " + string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents
            {whereClause};
            """;

        var dataSql = $"""
    SELECT
        id AS Id,
        client_entity_id AS ClientEntityId,
        type AS Type,
        document_number AS DocumentNumber,
        issue_date AS IssueDate,
        expiry_date AS ExpiryDate,
        file_url AS FileUrl,
        file_name AS FileName,
        content_type AS ContentType,
        file_size AS FileSize,
        is_active AS IsActive,
        is_deleted AS IsDeleted,
        created_at AS CreatedAt,
        updated_at AS UpdatedAt,

        {SqlSnippets.DocumentStatusNumericCase("expiry_date")}

    FROM documents
    {whereClause}
    ORDER BY created_at DESC;
    """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            ClientEntityId = clientEntityId
        };

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<
                ClientEntityDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientEntityDocumentsResponseDto(
            items.ToList(),
            totalCount);
    }
}