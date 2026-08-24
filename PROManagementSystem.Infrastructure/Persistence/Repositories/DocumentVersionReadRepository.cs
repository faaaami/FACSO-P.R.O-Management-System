using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentVersions;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DocumentVersionReadRepository
    : IDocumentVersionReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentVersionReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetDocumentVersionsResponseDto>
        GetByDocumentIdAsync(
            Guid documentId,
            bool includeDeleted,
            CancellationToken cancellationToken)
    {
        var whereConditions = new List<string>
        {
            "document_id = @DocumentId"
        };

        if (!includeDeleted)
        {
            whereConditions.Add(
                "is_deleted = false");
        }

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM document_versions
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                id AS Id,
                document_id AS DocumentId,
                version_number AS VersionNumber,
                file_url AS FileUrl,
                file_name AS FileName,
                content_type AS ContentType,
                file_size AS FileSize,
                is_deleted AS IsDeleted,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt
            FROM document_versions
            {whereClause}
            ORDER BY version_number DESC;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            DocumentId = documentId
        };

        var totalCount =
            await connection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    countSql,
                    parameters,
                    cancellationToken: cancellationToken));

        var items =
            await connection.QueryAsync<
                DocumentVersionListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetDocumentVersionsResponseDto(
            items.ToList(),
            totalCount);
    }
}