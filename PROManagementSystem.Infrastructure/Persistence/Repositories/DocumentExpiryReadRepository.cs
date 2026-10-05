using Dapper;
using Npgsql;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;
using PROManagementSystem.Infrastructure.Persistence.Sql;

namespace PROManagementSystem.Infrastructure.Persistence.Repositories;

public sealed class DocumentExpiryReadRepository
    : IDocumentExpiryReadRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public DocumentExpiryReadRepository(
        NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<GetExpiringDocumentsResponseDto>
        GetExpiringAsync(
            int days,
            int page,
            int pageSize,
            bool includeExpired,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            "is_deleted = false",

            // P10.4 / R-13: without this the proactive scanner fed inactive
            // documents to RenewalTaskCreationService, which refuses them by
            // design. The job caught that per document and logged one false
            // failure per inactive expiring document on every run. The Admin
            // expiring list shares this query, so the same predicate keeps
            // that list aligned with the dashboard, employee and overdue
            // queries, which all already filter is_active = true.
            "is_active = true",

            "expiry_date IS NOT NULL"
        };

        whereConditions.Add(
            SqlSnippets.ExpiryWindowCondition(
                "expiry_date",
                includeExpired));

        var whereClause =
            "WHERE " +
            string.Join(
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
                employee_id AS EmployeeId,
                type AS Type,
                document_number AS DocumentNumber,
                issue_date AS IssueDate,
                expiry_date AS ExpiryDate,
                file_name AS FileName,
                is_active AS IsActive,
                is_deleted AS IsDeleted,

                {SqlSnippets.DaysRemaining("expiry_date")},

                {SqlSnippets.ExpiryStatusLabelCase("expiry_date", 90)}

            FROM documents
            {whereClause}

            ORDER BY expiry_date ASC

            {SqlSnippets.Pagination};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            Days = days,
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
                ExpiringDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringDocumentsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetExpiringDocumentsResponseDto>
        GetExpiringByCompanyIdAsync(
            Guid clientCompanyId,
            int days,
            int page,
            int pageSize,
            bool includeExpired,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        var whereConditions = new List<string>
        {
            // Company-rooted tenancy predicate. The restriction is enforced
            // inside the SQL query, never after fetching all rows.
            $"({SqlSnippets.ClientDocumentScope})",
            "d.is_deleted = false",
            "d.expiry_date IS NOT NULL"
        };

        whereConditions.Add(
            SqlSnippets.ExpiryWindowCondition(
                "d.expiry_date",
                includeExpired));

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents d
            {whereClause};
            """;

        var dataSql = $"""
            SELECT
                d.id AS Id,
                d.client_entity_id AS ClientEntityId,
                d.employee_id AS EmployeeId,
                d.type AS Type,
                d.document_number AS DocumentNumber,
                d.issue_date AS IssueDate,
                d.expiry_date AS ExpiryDate,
                d.file_name AS FileName,
                d.is_active AS IsActive,
                d.is_deleted AS IsDeleted,

                {SqlSnippets.DaysRemaining("d.expiry_date")},

                {SqlSnippets.ExpiryStatusLabelCase("d.expiry_date", 90)}

            FROM documents d
            {whereClause}

            ORDER BY d.expiry_date ASC

            {SqlSnippets.Pagination};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            ClientCompanyId = clientCompanyId,
            Days = days,
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
                ExpiringDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringDocumentsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }

    public async Task<GetExpiringDocumentsResponseDto>
        GetRecentOverdueAsync(
            int days,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
    {
        var offset = (page - 1) * pageSize;

        // RenewalTaskCreationService throws InvalidOperationException for an
        // inactive document, so including inactive ones would guarantee one
        // logged failure per inactive document on every run. GetExpiringAsync
        // filters is_active for the same reason, as do
        // EmployeeExpiryReadRepository and both Dashboard expiry-alert
        // queries. What still distinguishes this query from its proactive
        // twin is the bounded past window, not the active filter.
        var whereConditions = new List<string>
        {
            "is_deleted = false",
            "expiry_date IS NOT NULL",
            "is_active = true"
        };

        whereConditions.Add(
            SqlSnippets.RecentOverdueWindowCondition(
                "expiry_date"));

        var whereClause =
            "WHERE " +
            string.Join(
                " AND ",
                whereConditions);

        var countSql = $"""
            SELECT COUNT(*)
            FROM documents
            {whereClause};
            """;

        // Same projection as GetExpiringAsync: the overdue sweep only needs
        // the id to hand to RenewalTaskCreationService, and reusing the DTO
        // keeps one row shape for every expiry read. DaysRemaining comes back
        // negative, which is the established signed convention rather than an
        // error, and ExpiryStatusLabelCase resolves to 'Expired' because its
        // first branch is the same < now test the window condition uses.
        var dataSql = $"""
            SELECT
                id AS Id,
                client_entity_id AS ClientEntityId,
                employee_id AS EmployeeId,
                type AS Type,
                document_number AS DocumentNumber,
                issue_date AS IssueDate,
                expiry_date AS ExpiryDate,
                file_name AS FileName,
                is_active AS IsActive,
                is_deleted AS IsDeleted,

                {SqlSnippets.DaysRemaining("expiry_date")},

                {SqlSnippets.ExpiryStatusLabelCase("expiry_date", 90)}

            FROM documents
            {whereClause}

            ORDER BY expiry_date ASC

            {SqlSnippets.Pagination};
            """;

        await using var connection =
            await _dataSource.OpenConnectionAsync(
                cancellationToken);

        var parameters = new
        {
            Days = days,
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
                ExpiringDocumentListItemDto>(
                new CommandDefinition(
                    dataSql,
                    parameters,
                    cancellationToken: cancellationToken));

        return new GetExpiringDocumentsResponseDto(
            items.ToList(),
            page,
            pageSize,
            totalCount);
    }
}