using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Common.Jobs;

/// <summary>
/// Overdue document recovery sweep.
///
/// This exists because DocumentExpiryJob structurally cannot see an expired
/// document: it calls GetExpiringAsync with includeExpired: false, whose
/// lower bound is <c>expiry_date &gt;= CURRENT_TIMESTAMP</c>. A document
/// that has already gone past its expiry date is therefore never a candidate,
/// so if a renewal task was never raised for it, or the task that existed was
/// closed, nothing in the system ever revisits it.
///
/// The tempting fix is to flip that call to includeExpired: true, and it is
/// the wrong one. That drops the lower bound instead of moving it, so the
/// scanner would reach back through every expired document on record and
/// re-raise it on every run, forever. Recovery has to be bounded to be worth
/// doing at all, so this is a separate job over a separate, explicitly
/// bounded window rather than a changed mode of the proactive one.
/// DocumentExpiryJob is untouched.
///
/// The window is the 30 days immediately after expiry. Inside it, this job
/// raises the same renewal task the proactive scanner would have raised, and
/// then the window closes behind it: a document that expires more than 30 days
/// ago stops being a candidate and is left to the ordinary admin workflow. An
/// overdue document is therefore surfaced once and promptly, not
/// indefinitely.
///
/// Task creation is delegated wholesale to IRenewalTaskCreationService, which
/// is the existing authority for whether a task may be created and what one
/// looks like. This job decides only which documents to offer; the service
/// decides which of those become tasks. That is what makes duplicates a
/// non-event: it returns null for a document that already has a live renewal
/// task, and absorbs the unique-index race the same way, so a repeated or
/// overlapping run is a quiet no-op rather than an error. Nothing here
/// inspects task status, so an Updated task from a previous renewal cycle
/// does not suppress this one — a recurring renewal must stay able to open a
/// new cycle.
/// </summary>
public sealed class DocumentOverdueSweepJob
{
    /// <summary>
    /// How far past expiry a document may be and still be recovered. Fixed
    /// rather than configured: it is a product rule about how long a document
    /// may sit expired before it is no longer recoverable by this sweep, and
    /// it is deliberately not adjustable per request the way the
    /// user-facing expiry queries allow.
    /// </summary>
    private const int OverdueWindowDays = 30;

    private const int PageSize = 100;

    private readonly IDocumentExpiryReadRepository _documentRepository;
    private readonly IRenewalTaskCreationService _renewalTaskService;
    private readonly ILogger<DocumentOverdueSweepJob> _logger;
    private readonly Guid _automationUserId;

    public DocumentOverdueSweepJob(
        IDocumentExpiryReadRepository documentRepository,
        IRenewalTaskCreationService renewalTaskService,
        ILogger<DocumentOverdueSweepJob> logger,
        Guid automationUserId)
    {
        _documentRepository = documentRepository;
        _renewalTaskService = renewalTaskService;
        _logger = logger;
        _automationUserId = automationUserId;
    }

    public async Task ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Document overdue sweep started at {Time} " +
            "({WindowDays}-day recovery window).",
            DateTime.UtcNow,
            OverdueWindowDays);

        var page = 1;

        var createdCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var processedCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result =
                await _documentRepository.GetRecentOverdueAsync(
                    OverdueWindowDays,
                    page,
                    PageSize,
                    cancellationToken);

            if (result.Items.Count == 0)
            {
                break;
            }

            _logger.LogInformation(
                "Processing overdue page {Page}. Documents on page: {Count}.",
                page,
                result.Items.Count);

            foreach (var document in result.Items)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedCount++;

                try
                {
                    var taskId =
                        await _renewalTaskService.CreateAsync(
                            document.Id,
                            _automationUserId,
                            cancellationToken);

                    if (taskId.HasValue)
                    {
                        createdCount++;

                        _logger.LogInformation(
                            "Overdue renewal task {TaskId} created for expired document {DocumentId}.",
                            taskId.Value,
                            document.Id);
                    }
                    else
                    {
                        // Same outcome for both reasons the service declines:
                        // it already saw a live task, or it lost the
                        // unique-index race to another run. Neither is an
                        // error at this level.
                        skippedCount++;

                        _logger.LogInformation(
                            "Skipped overdue document {DocumentId} because an active renewal task already exists.",
                            document.Id);
                    }
                }
                catch (Exception ex)
                {
                    // Per-document isolation, matching DocumentExpiryJob: a
                    // document the service refuses must not strand every
                    // remaining candidate in the run, or one bad row would cost
                    // the whole sweep its coverage.
                    //
                    // Nothing in the current schema can trigger this — the
                    // query already filters the deleted and inactive documents
                    // the service rejects, and a company always resolves
                    // because the owner id is enforced by a check constraint
                    // and read with IgnoreQueryFilters(). That makes this a
                    // guard against a future rejection rule, not a routine
                    // path, and it is left in place deliberately: it costs
                    // nothing per document and it is the only thing standing
                    // between one refusal and a silently truncated run.
                    failedCount++;

                    _logger.LogError(
                        ex,
                        "Failed to process overdue document {DocumentId}.",
                        document.Id);
                }
            }

            // Last page reached. Paging stays stable across pages because this
            // job only ever writes renewal_tasks, never the documents table it
            // is reading.
            if (result.Items.Count < PageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation(
            "Document overdue sweep completed. " +
            "Documents processed: {ProcessedCount}, " +
            "tasks created: {CreatedCount}, " +
            "skipped: {SkippedCount}, " +
            "failed: {FailedCount}.",
            processedCount,
            createdCount,
            skippedCount,
            failedCount);
    }
}
