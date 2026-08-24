using Microsoft.Extensions.Logging;
using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Application.Common.Jobs;

public sealed class DocumentExpiryJob
{
    private readonly IDocumentExpiryReadRepository _documentRepository;
    private readonly IRenewalTaskCreationService _renewalTaskService;
    private readonly ILogger<DocumentExpiryJob> _logger;
    private readonly Guid _automationUserId;

    public DocumentExpiryJob(
        IDocumentExpiryReadRepository documentRepository,
        IRenewalTaskCreationService renewalTaskService,
        ILogger<DocumentExpiryJob> logger,
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
            "Document expiry scanner started at {Time}.",
            DateTime.UtcNow);

        const int days = 90;
        const int pageSize = 100;

        var page = 1;

        var createdCount = 0;
        var skippedCount = 0;
        var failedCount = 0;
        var processedCount = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var result =
                await _documentRepository.GetExpiringAsync(
                    days,
                    page,
                    pageSize,
                    includeExpired: false,
                    cancellationToken);

            if (result.Items.Count == 0)
            {
                break;
            }

            _logger.LogInformation(
                "Processing expiry page {Page}. Documents on page: {Count}.",
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
                            "Renewal task {TaskId} created automatically for document {DocumentId}.",
                            taskId.Value,
                            document.Id);
                    }
                    else
                    {
                        skippedCount++;

                        _logger.LogInformation(
                            "Skipped document {DocumentId} because an active renewal task already exists.",
                            document.Id);
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;

                    _logger.LogError(
                        ex,
                        "Failed to process expiring document {DocumentId}.",
                        document.Id);
                }
            }

            // Last page reached.
            if (result.Items.Count < pageSize)
            {
                break;
            }

            page++;
        }

        _logger.LogInformation(
            "Document expiry scanner completed. " +
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