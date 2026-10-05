using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Classifies the text recovered by the OCR/PDF extraction pipeline into a
/// suggested document type. The classifier consumes only in-memory text that
/// stays internal to the pipeline; it must never surface OCR text through the
/// API, storage, or logs.
/// </summary>
public interface IDocumentClassifier
{
    Task<DocumentClassificationResult> ClassifyAsync(
        string? text,
        CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of document-type classification. <see cref="Type"/> is
/// <see langword="null"/> when there is no sufficient evidence to choose a
/// type — an unknown, ambiguous, or low-confidence document — and
/// <see cref="Confidence"/> is 0. The classifier never invents an "Other"
/// label; <see cref="DocumentType.Other"/> remains a human decision at
/// confirmation time.
/// </summary>
public sealed record DocumentClassificationResult(
    DocumentType? Type,
    decimal Confidence);
