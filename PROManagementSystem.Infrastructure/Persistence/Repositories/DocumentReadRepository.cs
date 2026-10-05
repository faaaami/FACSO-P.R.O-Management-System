using Dapper;
using Npgsql;
using PROManagementSystem.Application.Client.Queries.GetMyDocuments;
using PROManagementSystem.Application.Common.Helpers;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetDocumentById;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DocumentReadRepository
    : IDocumentReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetDocumentByIdResponseDto?> GetByIdAsync(
        Guid documentId,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                id AS Id,
                client_entity_id AS ClientEntityId,
                employee_id AS EmployeeId,
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
                deleted_at AS DeletedAt,
                created_at AS CreatedAt,
                updated_at AS UpdatedAt,

                {SqlSnippets.DocumentStatusNumericCase("expiry_date")},
                details AS Details

            FROM documents
            WHERE id = @DocumentId
              AND (
                    @IncludeDeleted = true
                    OR is_deleted = false
                  );
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var row =
            await connection.QuerySingleOrDefaultAsync<
                GetDocumentByIdRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        DocumentId = documentId,
                        IncludeDeleted = includeDeleted
                    },
                    cancellationToken: cancellationToken));

        return MapToDto(row);
    }

    public async Task<GetDocumentByIdResponseDto?> GetByIdForCompanyAsync(
        Guid clientCompanyId,
        Guid documentId,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT
                d.id AS Id,
                d.client_entity_id AS ClientEntityId,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_url AS FileUrl,
                d.file_name AS FileName,
                d.content_type AS ContentType,
                d.file_size AS FileSize,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                d.deleted_at AS DeletedAt,
                d.created_at AS CreatedAt,
                d.updated_at AS UpdatedAt,

                {SqlSnippets.DocumentStatusNumericCase("d.expiry_date")},
                d.details AS Details

            FROM documents d
            WHERE d.id = @DocumentId
              AND d.is_deleted = false
              AND ({SqlSnippets.ClientDocumentScope})
            LIMIT 1;
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var row =
            await connection.QuerySingleOrDefaultAsync<
                GetDocumentByIdRow>(
                new CommandDefinition(
                    sql,
                    new
                    {
                        ClientCompanyId = clientCompanyId,
                        DocumentId = documentId
                    },
                    cancellationToken: cancellationToken));

        return MapToDto(row);
    }

    /// <summary>
    /// Dapper materializes the jsonb column as a CLR string. Map it to the API
    /// DTO as a JSON object so the response serializes <c>details</c> as an
    /// object rather than an escaped string.
    /// </summary>
    private static GetDocumentByIdResponseDto? MapToDto(
        GetDocumentByIdRow? row)
    {
        if (row is null)
        {
            return null;
        }

        return new GetDocumentByIdResponseDto(
            row.Id,
            row.ClientEntityId,
            row.EmployeeId,
            row.Type,
            row.DocumentNumber,
            row.IssueDate,
            row.ExpiryDate,
            row.FileUrl,
            row.FileName,
            row.ContentType,
            row.FileSize,
            row.IsActive,
            row.IsDeleted,
            row.DeletedAt,
            row.CreatedAt,
            row.UpdatedAt,
            (DocumentStatus)row.Status,
            DetailsJson.FromStorage(row.Details));
    }

    /// <summary>
    /// Dapper-friendly projection of the document row. <c>Details</c> is the
    /// raw jsonb string; the conversion to a JSON object happens in
    /// <see cref="MapToDto"/>. <c>Status</c> is the numeric status computed by
    /// the SQL CASE expression.
    /// </summary>
    private sealed class GetDocumentByIdRow
    {
        public Guid Id { get; set; }

        public Guid? ClientEntityId { get; set; }

        public Guid? EmployeeId { get; set; }

        public DocumentType Type { get; set; }

        public string DocumentNumber { get; set; } = string.Empty;

        public DateTime? IssueDate { get; set; }

        public DateTime? ExpiryDate { get; set; }

        public string? FileUrl { get; set; }

        public string? FileName { get; set; }

        public string? ContentType { get; set; }

        public long? FileSize { get; set; }

        public bool IsActive { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public int Status { get; set; }

        public string Details { get; set; } = "{}";
    }

    public async Task<GetClientDocumentsResponseDto> GetByCompanyIdAsync(
        Guid clientCompanyId,
        DocumentType? type,
        DocumentStatus? status,
        DateTime? expiresFrom,
        DateTime? expiresTo,
        DocumentOwnerType? ownerType,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            // Company-rooted tenancy predicate. A document is visible only
            // when the authenticated client company owns its entity or
            // employee. The restriction is enforced inside the SQL query.
            $"({SqlSnippets.ClientDocumentScope})",
            "d.is_deleted = false"
        };

        var parameters = new DynamicParameters();
        parameters.Add("ClientCompanyId", clientCompanyId);

        if (type.HasValue)
        {
            whereConditions.Add("d.type = @Type");
            parameters.Add("Type", type.Value.ToString());
        }

        if (status.HasValue)
        {
            switch (status.Value)
            {
                case DocumentStatus.Active:
                    // The disjunction is wrapped so the whole Active predicate
                    // stays ONE atom inside the AND chain. Without the outer
                    // parentheses, SQL precedence binds the surrounding ANDs
                    // tighter and the "> 90 days" branch escapes the company
                    // scope and the soft-delete filter entirely.
                    whereConditions.Add(
                        "(d.expiry_date IS NULL OR " +
                        $"{SqlSnippets.BeyondWindowClause("d.expiry_date", 90)})");
                    break;

                case DocumentStatus.ExpiringSoon:
                    whereConditions.Add(
                        "d.expiry_date IS NOT NULL AND " +
                        "d.expiry_date > CURRENT_TIMESTAMP AND " +
                        SqlSnippets.ExpiryWindowClause("d.expiry_date", 90));
                    break;

                case DocumentStatus.Overdue:
                    whereConditions.Add(
                        "d.expiry_date IS NOT NULL AND " +
                        "d.expiry_date < CURRENT_TIMESTAMP");
                    break;

                case DocumentStatus.InRenewal:
                    whereConditions.Add(
                        "EXISTS (SELECT 1 FROM renewal_tasks rt " +
                        $"WHERE rt.\"DocumentId\" = d.id " +
                        $"AND rt.\"Status\" IN (1, 2, 3, 5) " +
                        $"AND rt.\"IsDeleted\" = false)");
                    break;
            }
        }

        if (expiresFrom.HasValue)
        {
            whereConditions.Add("d.expiry_date >= @ExpiresFrom");
            parameters.Add("ExpiresFrom", expiresFrom.Value);
        }

        if (expiresTo.HasValue)
        {
            whereConditions.Add("d.expiry_date <= @ExpiresTo");
            parameters.Add("ExpiresTo", expiresTo.Value);
        }

        if (ownerType.HasValue)
        {
            whereConditions.Add(
                ownerType.Value == DocumentOwnerType.Entity
                    ? "d.client_entity_id IS NOT NULL"
                    : "d.employee_id IS NOT NULL");
        }

        var whereClause =
            "WHERE " + string.Join(" AND ", whereConditions);

        var countSql = $"""
            SELECT COUNT(DISTINCT d.id)
            FROM documents d
            {whereClause};
            """;

        var dataSql = $"""
            SELECT DISTINCT
                d.id AS Id,
                d.client_entity_id AS ClientEntityId,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_url AS FileUrl,
                d.file_name AS FileName,
                d.content_type AS ContentType,
                d.file_size AS FileSize,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,
                d.created_at AS CreatedAt,
                d.updated_at AS UpdatedAt,
                {SqlSnippets.DocumentStatusNumericCase("d.expiry_date")}
            FROM documents d
            {whereClause}
            ORDER BY d.created_at DESC
            {SqlSnippets.Pagination};
            """;

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

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
            await connection.QueryAsync<ClientDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetClientDocumentsResponseDto(
            items.ToList(), page, pageSize, totalCount);
    }
}