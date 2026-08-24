using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI;

public sealed class MockDocumentExtractionService
    : IDocumentExtractionService
{
    public async Task<DocumentExtractionResult> ExtractAsync(
        Stream document,
        string contentType,
        CancellationToken cancellationToken)
    {
        // Simulate AI processing time
        await Task.Delay(
            TimeSpan.FromMilliseconds(500),
            cancellationToken);

        return new DocumentExtractionResult(
            Type: DocumentType.Passport,
            DocumentNumber: "AI-PASS-123456",
            IssueDate: new DateTime(
                2026,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),
            ExpiryDate: new DateTime(
                2031,
                1,
                1,
                0,
                0,
                0,
                DateTimeKind.Utc),
            OwnerType: "Employee",
            Confidence: 0.96m
        );
    }
}