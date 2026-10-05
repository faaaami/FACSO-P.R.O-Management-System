using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Entities;

namespace PROManagementSystem.Application.Common.Jobs;

/// <summary>
/// Background reconciliation for expired document extractions. Rows that are
/// still Pending and past their ExpiresAt are transitioned to Expired (with a
/// single DocumentExtractionExpired audit under the automation actor) and only
/// then is the stored file deleted - best-effort and idempotent, so a failed
/// delete never rolls back the transition and is retried on the next run.
/// Confirmed, already-Expired, NULL-ExpiresAt and soft-deleted rows are never
/// touched, and the soft-delete query filter automatically hides deleted rows.
/// </summary>
public sealed class ExtractionExpiryCleanupJob
{
    private const int PageSize = 100;

    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly ILogger<ExtractionExpiryCleanupJob> _logger;
    private readonly Guid _automationUserId;

    public ExtractionExpiryCleanupJob(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        ILogger<ExtractionExpiryCleanupJob> logger,
        Guid automationUserId)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _logger = logger;
        _automationUserId = automationUserId;
    }

    public async Task ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Extraction expiry cleanup started at {Time}.",
            DateTime.UtcNow);

        var now = DateTime.UtcNow;

        var page = 1;

        var processedCount = 0;
        var expiredCount = 0;
        var skippedCount = 0;
        var deletedCount = 0;
        var deleteFailedCount = 0;
        var failedCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var batch =
                await _context.DocumentExtractions
                    .Where(x =>
                        x.Status == "Pending" &&
                        x.ExpiresAt.HasValue &&
                        x.ExpiresAt.Value <= now)
                    .OrderBy(x => x.CreatedAt)
                    .Skip((page - 1) * PageSize)
                    .Take(PageSize)
                    .ToListAsync(cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            _logger.LogInformation(
                "Processing extraction expiry page {Page}. " +
                "Extractions on page: {Count}.",
                page,
                batch.Count);

            foreach (var extraction in batch)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedCount++;

                try
                {
                    // Never touch a row another process already transitioned
                    // since the query ran.
                    if (extraction.Status != "Pending")
                    {
                        skippedCount++;
                        continue;
                    }

                    extraction.Status = "Expired";
                    extraction.UpdatedAt = now;

                    _context.AuditLogs.Add(new AuditLog
                    {
                        Id = Guid.NewGuid(),
                        UserId = _automationUserId,
                        Action = "DocumentExtractionExpired",
                        EntityType = "DocumentExtraction",
                        EntityId = extraction.Id,
                        Description =
                            "Document extraction has expired.",
                        CreatedAt = now,
                        UpdatedAt = now,
                        IsDeleted = false,
                        DeletedAt = null
                    });

                    // Persist Pending -> Expired + audit BEFORE touching the
                    // file. A failure here leaves the row untouched.
                    await _context.SaveChangesAsync(
                        cancellationToken);

                    expiredCount++;

                    // Physical file cleanup is best-effort and runs only
                    // after the Expired state has been committed, so a
                    // failed delete never rolls back the transition and the
                    // idempotent delete is simply retried on a later run.
                    if (!string.IsNullOrWhiteSpace(extraction.FileUrl))
                    {
                        try
                        {
                            await _fileStorageService.DeleteAsync(
                                extraction.FileUrl,
                                cancellationToken);

                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            deleteFailedCount++;

                            _logger.LogError(
                                ex,
                                "Failed to delete the stored file for " +
                                "expired extraction {ExtractionId}. " +
                                "The file will be retried on a later run.",
                                extraction.Id);
                        }
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;

                    _logger.LogError(
                        ex,
                        "Failed to expire extraction {ExtractionId}.",
                        extraction.Id);
                }
            }

            // Last page reached.
            if (batch.Count < PageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation(
            "Extraction expiry cleanup completed. " +
            "Extractions processed: {ProcessedCount}, " +
            "expired: {ExpiredCount}, skipped: {SkippedCount}, " +
            "files deleted: {DeletedCount}, " +
            "file delete failures: {DeleteFailedCount}, " +
            "failed: {FailedCount}.",
            processedCount,
            expiredCount,
            skippedCount,
            deletedCount,
            deleteFailedCount,
            failedCount);
    }
}