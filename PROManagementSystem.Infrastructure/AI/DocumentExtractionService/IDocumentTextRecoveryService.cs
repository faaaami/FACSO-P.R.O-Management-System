namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1: recovers the document text that feeds the
/// <see cref="OpenRouterPromptBuilder"/>. Implemented by
/// <see cref="DocumentTextRecoveryService"/> which reuses the existing PDF-text
/// / scanned-PDF OCR / raster-image OCR components under the workload gate and
/// deadline. The recovered text stays internal to the pipeline.
/// </summary>
internal interface IDocumentTextRecoveryService
{
    Task<string?> RecoverAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken);
}