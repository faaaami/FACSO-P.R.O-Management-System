using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Application.Common.Interfaces;

/// <summary>
/// Failure codes and the messages shown to staff on the review form. The
/// upload itself never fails because of extraction: the reviewer can always
/// enter the values by hand.
/// </summary>
public static class ExtractionFailures
{
    public const string MrzNotFound = "MRZ_NOT_FOUND";
    public const string MrzCheckFailed = "MRZ_CHECK_FAILED";
    public const string ImageUnreadable = "IMAGE_UNREADABLE";
    public const string ImageTooSmall = "IMAGE_TOO_SMALL";
    public const string OcrTimeout = "OCR_TIMEOUT";
    public const string NothingRead = "NOTHING_READ";
    public const string AiDisabled = "AI_DISABLED";
    public const string AiUnavailable = "AI_UNAVAILABLE";

    public static ExtractionFailure MrzNotFoundFor(DocumentType type) => new(
        MrzNotFound,
        type == DocumentType.EmiratesId
            ? "MRZ not found. Please upload a clearer photo of the back side of the Emirates ID, " +
              "including the three lines of letters, numbers and < symbols at the bottom."
            : "MRZ not found. Please upload a clearer photo of the passport photo page, " +
              "including the two lines of letters, numbers and < symbols at the bottom.");

    public static ExtractionFailure MrzCheckFailedFailure() => new(
        MrzCheckFailed,
        "The MRZ was read, but some check digits don't match. " +
        "The highlighted fields need checking against the document.");

    public static ExtractionFailure Unreadable() => new(
        ImageUnreadable,
        "The file could not be read as an image or PDF. Please upload a JPG, PNG, HEIC or PDF.");

    public static ExtractionFailure TooSmall() => new(
        ImageTooSmall,
        "The image is too small to read. Please upload a photo at least 1000 pixels wide.");

    public static ExtractionFailure Timeout() => new(
        OcrTimeout,
        "Reading the document took too long. Please enter the details manually or try a smaller file.");

    public static ExtractionFailure Nothing() => new(
        NothingRead,
        "No text could be read from this document. Please enter the details manually, " +
        "or upload a sharper, well-lit photo or scan.");

    public static ExtractionFailure Disabled() => new(
        AiDisabled,
        "Automatic reading is not enabled for this document type. Please enter the details.");

    public static ExtractionFailure Unavailable() => new(
        AiUnavailable,
        "The reading service is unavailable right now. Please enter the details manually.");
}
