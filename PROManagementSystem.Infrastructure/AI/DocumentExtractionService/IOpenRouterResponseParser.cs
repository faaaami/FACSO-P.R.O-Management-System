using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentExtractionService;

/// <summary>
/// Task 7C.3.1 boundary between the OpenRouter HTTP transport and the
/// structured-response parser: a provider receives one raw
/// <see cref="OpenRouterRawResponse"/> and produces the document-extraction
/// result. Task 7C.3.2 implements the real mapping in
/// <see cref="OpenRouterResponseParser"/>; the raw response is never copied
/// into the result and is never logged.
/// </summary>
internal interface IOpenRouterResponseParser
{
    AiParsedExtraction Parse(
        OpenRouterRawResponse raw,
        DocumentType? detectedType,
        decimal classificationConfidence);
}
