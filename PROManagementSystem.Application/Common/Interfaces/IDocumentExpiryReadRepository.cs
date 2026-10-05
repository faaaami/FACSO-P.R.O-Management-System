using PROManagementSystem.Application.Documents.Queries.GetExpiringDocuments;

namespace PROManagementSystem.Application.Common.Interfaces;

public interface IDocumentExpiryReadRepository
{
    Task<GetExpiringDocumentsResponseDto> GetExpiringAsync(
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken);
    Task<GetExpiringDocumentsResponseDto> GetExpiringByCompanyIdAsync(
        Guid clientCompanyId,
        int days,
        int page,
        int pageSize,
        bool includeExpired,
        CancellationToken cancellationToken);

    /// <summary>
    /// Documents that are already expired but no more than
    /// <paramref name="days"/> days past expiry, across all companies.
    ///
    /// This is a distinct query rather than another
    /// <c>includeExpired</c> mode because the two questions are different.
    /// GetExpiringAsync(includeExpired: false) asks what is coming up and
    /// cannot see an expired document at all; GetExpiringAsync(
    /// includeExpired: true) drops the lower bound and so returns every
    /// expired document ever recorded. Only this one bounds the past, which
    /// is what makes overdue recovery a bounded sweep rather than an
    /// indefinite re-raise of historical documents.
    ///
    /// Unscoped by design, matching GetExpiringAsync: it feeds a system-wide
    /// maintenance job, and per-document company resolution belongs to
    /// RenewalTaskCreationService, which refuses documents it cannot
    /// attribute to a company.
    /// </summary>
    Task<GetExpiringDocumentsResponseDto> GetRecentOverdueAsync(
        int days,
        int page,
        int pageSize,
        CancellationToken cancellationToken);
}