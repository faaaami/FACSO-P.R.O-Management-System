namespace PROManagementSystem.Infrastructure.Persistence.Sql;

// Centralized PostgreSQL fragments used by the Dapper read repositories.
// Call-sites must keep each fragment's original semantics (window values,
// alias names, output labels) — deliberate divergences are marked inline.
internal static class SqlSnippets
{
    // Agent task-linked scope predicate. The renewal_tasks alias MUST be rt.
    public const string AgentTaskScope =
        "rt.\"AssignedStaffId\" = @AgentId AND rt.\"IsDeleted\" = false";

    // Strict task-linked document scope subquery (documents alias MUST be d,
    // renewal_tasks alias MUST be rt).
    public const string DocumentScopeExists =
        "EXISTS (SELECT 1 FROM renewal_tasks rt " +
        "WHERE rt.\"DocumentId\" = d.id " +
        "AND rt.\"AssignedStaffId\" = @AgentId " +
        "AND rt.\"IsDeleted\" = false)";

    // Client company-rooted document scope predicate (documents alias MUST
    // be d). A document belongs to the tenant when its owning entity, or the
    // owning employee's entity, belongs to the authenticated ClientCompanyId.
    // Soft-deleted owners are excluded from the tenant's view.
    public const string ClientDocumentScope =
        "(d.client_entity_id IS NOT NULL AND EXISTS (" +
        "SELECT 1 FROM client_entities ce " +
        "WHERE ce.id = d.client_entity_id " +
        "AND ce.client_company_id = @ClientCompanyId " +
        "AND ce.is_deleted = false)) " +
        "OR (d.employee_id IS NOT NULL AND EXISTS (" +
        "SELECT 1 FROM employees e " +
        "INNER JOIN client_entities ce ON ce.id = e.client_entity_id " +
        "WHERE e.id = d.employee_id " +
        "AND ce.client_company_id = @ClientCompanyId " +
        "AND e.is_deleted = false " +
        "AND ce.is_deleted = false))";

    // Client company-rooted employee scope predicate (employees alias MUST be
    // e, client_entities alias MUST be ce).
    public const string ClientEmployeeScope =
        "ce.client_company_id = @ClientCompanyId " +
        "AND e.is_deleted = false " +
        "AND ce.is_deleted = false";

    // Numeric document status classification (1 active, 2 expiring soon,
    // 3 overdue). Used by document list/detail read models.
    public static string DocumentStatusNumericCase(string expiryColumn) =>
        $"""
        CASE
            WHEN {expiryColumn} IS NULL THEN 1
            WHEN {expiryColumn} < CURRENT_TIMESTAMP THEN 3
            WHEN {expiryColumn} <= CURRENT_TIMESTAMP + INTERVAL '90 days' THEN 2
            ELSE 1
        END AS Status
        """;

    // Label document status classification. The 90-day window is the single
    // shared expiry window used by the expiry job, the read repositories and
    // the numeric case above; every caller passes 90.
    public static string ExpiryStatusLabelCase(string expiryColumn, int windowDays) =>
        $"""
        CASE
            WHEN {expiryColumn} < CURRENT_TIMESTAMP THEN 'Expired'
            WHEN {expiryColumn} <= CURRENT_TIMESTAMP + INTERVAL '{windowDays} days' THEN 'ExpiringSoon'
            ELSE 'Active'
        END AS Status
        """;

    // Single-window comparison clause: {expiryColumn} <= now + N days.
    public static string ExpiryWindowClause(string expiryColumn, int days) =>
        $"{expiryColumn} <= CURRENT_TIMESTAMP + INTERVAL '{days} days'";

    // Inverse comparison clause: {expiryColumn} > now + N days.
    public static string BeyondWindowClause(string expiryColumn, int days) =>
        $"{expiryColumn} > CURRENT_TIMESTAMP + INTERVAL '{days} days'";

    // Parameterized expiring-items window using the @Days parameter. The
    // includeExpired=false twin also requires {expiryColumn} >= now.
    public static string ExpiryWindowCondition(string expiryColumn, bool includeExpired) =>
        includeExpired
            ? $"{expiryColumn} <= CURRENT_TIMESTAMP + (@Days * INTERVAL '1 day')"
            : $"{expiryColumn} >= CURRENT_TIMESTAMP AND {expiryColumn} <= CURRENT_TIMESTAMP + (@Days * INTERVAL '1 day')";

    // Bounded recent-overdue window, the mirror image of
    // ExpiryWindowCondition(includeExpired: false).
    //
    // That twin cannot be reused for overdue recovery: with
    // includeExpired = false its lower bound is
    // {expiryColumn} >= CURRENT_TIMESTAMP, which makes an already-expired
    // document structurally invisible, and with includeExpired = true it
    // drops the lower bound entirely and reaches back through all of
    // history. Neither expresses "recently went past", which is the only
    // thing an overdue recovery sweep may act on — a document that expired
    // years ago has already been given whatever recovery it was going to
    // get, and re-raising it every run is noise, not recovery.
    //
    // So the window is stated here rather than derived from the existing
    // helpers, and it is deliberately not a negation: the lower bound is
    // @Days back from now (inclusive, matching the >= lower edge of
    // ExpiryWindowCondition) and the upper edge is now (exclusive, matching
    // the DocumentStatus.Overdue predicate in DocumentReadRepository).
    // @Days is supplied as a Dapper parameter, exactly as it is for
    // ExpiryWindowCondition.
    public static string RecentOverdueWindowCondition(
        string expiryColumn) =>
        $"{expiryColumn} >= CURRENT_TIMESTAMP - (@Days * INTERVAL '1 day') AND " +
        $"{expiryColumn} < CURRENT_TIMESTAMP";

    // Whole-days remaining until expiry (FLOOR of the exact interval).
    public static string DaysRemaining(string expiryColumn) =>
        $"""
        FLOOR(
            EXTRACT(
                EPOCH FROM
                ({expiryColumn} - CURRENT_TIMESTAMP)
            ) / 86400
        )::int AS DaysRemaining
        """;

    // Uniform pagination clause. The four legacy OFFSET/LIMIT spellings and
    // the OFFSET ROWS/FETCH NEXT spellings are all equivalent in PostgreSQL.
    public const string Pagination =
        "OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY";
}