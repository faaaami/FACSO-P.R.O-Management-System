using PROManagementSystem.Application.Common.Interfaces;

namespace PROManagementSystem.Infrastructure.AI;

/// <summary>
/// Deterministic provider for development and tests (Extraction:Provider =
/// Mock): returns fixed placeholder values for the selected type without
/// reading the file.
/// </summary>
public sealed class MockDocumentExtractionService
    : IDocumentExtractionService
{
    public const string DocumentNumber = "AI-PASS-123456";
    public const string IssueDate = "2026-01-01";
    public const string ExpiryDate = "2031-01-01";

    public async Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken)
    {
        // Simulate processing time.
        await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);

        return new DocumentExtractionResult(
            request.DocumentType,
            new Dictionary<string, ExtractedField>
            {
                [DocumentExtractionResult.DocumentNumberKey] =
                    ExtractedField.Read(DocumentNumber, ExtractedFieldSource.Ocr),
                [DocumentExtractionResult.IssueDateKey] =
                    ExtractedField.Read(IssueDate, ExtractedFieldSource.Ocr),
                [DocumentExtractionResult.ExpiryDateKey] =
                    ExtractedField.Read(ExpiryDate, ExtractedFieldSource.Ocr)
            },
            Failure: null);
    }
}
