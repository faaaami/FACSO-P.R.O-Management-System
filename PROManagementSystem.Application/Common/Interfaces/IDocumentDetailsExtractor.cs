using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Task 7B.5: extracts type-specific field suggestions from the text the
/// extraction pipeline recovered (OCR / PDF text layer) into a
/// schema-defined key/value object destined for
/// <see cref="Entities.DocumentExtraction.SuggestedDetails"/>. The
/// suggested document type and its schema registry constrain which fields are
/// produced; values are typed (strings, strict <c>yyyy-MM-dd</c> dates,
/// numbers, booleans) and never normalized beyond the documented policy.
///
/// Implementations must:
/// - consume only pipeline-internal text and never surface it through the
///   API, storage, or logs;
/// - emit only schema-defined keys and ignore every other label;
/// - treat partial extraction as valid (optional and even required fields may
///   be missing — required validation is a confirm-time concern);
/// - drop individual invalid candidate values rather than fail;
/// - propagate unexpected exceptions and cancellation rather than degrade.
///
/// The result is a suggestion only: it is never authoritative and must never
/// automatically become <see cref="Entities.Document.Details"/>. No
/// extraction confidence is produced here; classification confidence is owned
/// separately by the classifier and stays the sole persisted confidence.
/// </summary>
public interface IDocumentDetailsExtractor
{
    Task<DocumentDetailsExtractionResult> ExtractAsync(
        string? text,
        DocumentType? suggestedType,
        CancellationToken cancellationToken);
}

/// <summary>
/// The outcome of deterministic type-specific field extraction. An empty
/// <see cref="Details"/> means "no valid suggestions" and is persisted as the
/// canonical empty object <c>{}</c>. Keys follow the schema registry's
/// <see cref="DocumentSchemas.DocumentFieldDefinition.Key"/> contract of the
/// suggested type.
/// </summary>
public sealed record DocumentDetailsExtractionResult(
    IReadOnlyDictionary<string, object?> Details
);