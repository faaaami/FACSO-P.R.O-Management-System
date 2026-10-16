namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Synthetic, anonymised classification fixtures (Task 6C). Every value is
/// clearly fake; no real identity documents or numbers are used. These
/// fixtures prove the deterministic classification mechanics and wiring — not
/// real-world classification accuracy.
/// </summary>
internal static class TestDocumentFixtures
{
    internal const string SecretMarker = "FASCO-SECRET-6C-9D2B";

    /// <summary>
    /// Full passport fixture lines — each line becomes a PDF page via
    /// <c>TestPdfBuilder.CreateTextPdf(params)</c>.
    /// </summary>
    internal static string[] PassportLines() => new[]
    {
        "PASSPORT",
        "PASSPORT NUMBER: P-90321",
        "NATIONALITY: TESTLAND",
        "DATE OF BIRTH: 1990-01-01",
        "SURNAME: FAKEHOLDER",
        SecretMarker,
    };

    internal static string[] VisaLines() => new[]
    {
        "VISA",
        "RESIDENCE VISA",
        "VISA NUMBER: V-90321",
        "ENTRY PERMIT: EP-2026-01",
        "PASSPORT NUMBER: P-90321",
    };

    internal static string[] EmiratesIdLines() => new[]
    {
        "EMIRATES ID",
        "EMIRATES IDENTITY CARD",
        "ID NUMBER: 784-1990-1234567-6",
        "IDENTITY CARD NUMBER: 784-1990-1234567-6",
        "NATIONALITY: TESTLAND",
        "DATE OF BIRTH: 1990-01-01",
    };

    internal static string[] LaborCardLines() => new[]
    {
        "LABOR CARD",
        "LABOUR CARD",
        "LABOR CARD NUMBER: LC-2026-0001",
        "LABOUR CARD NUMBER: LC-2026-0001",
        "NATIONALITY: TESTLAND",
        "DATE OF BIRTH: 1990-01-01",
    };

    internal static string[] TradeLicenseLines() => new[]
    {
        "TRADE LICENSE",
        "TRADE LICENCE",
        "LICENSE NUMBER: TL-2026-0001",
        "LICENCE NUMBER: TL-2026-0001",
        "ECONOMIC DEPARTMENT",
    };

    internal static string[] EstablishmentCardLines() => new[]
    {
        "ESTABLISHMENT CARD",
        "ESTABLISHMENT NUMBER: EC-2026-0001",
        "ECONOMIC DEPARTMENT",
        "COMPANY NAME: TEST ESTABLISHMENT LLC",
    };

    internal static string[] UnknownDocumentLines() => new[]
    {
        "QUARANTINE CLEARANCE CERTIFICATE",
        "CASE NUMBER: 99-102-888",
        "RESULT: CLEARED",
    };

    /// <summary>
    /// Short text for image OCR fixtures (rendered at smaller font on a wider
    /// canvas so all lines fit reliably for Tesseract recognition).
    /// </summary>
    internal static string PassportImageText() =>
        string.Join("\n", PassportLines());

    internal static string EmiratesIdImageText() =>
        string.Join("\n", EmiratesIdLines());
}
