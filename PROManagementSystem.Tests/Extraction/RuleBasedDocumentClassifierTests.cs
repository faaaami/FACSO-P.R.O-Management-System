using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Unit tests for <see cref="RuleBasedDocumentClassifier"/> (Task 6C). Every
/// fixture uses synthetic, anonymised text with no real identity documents or
/// numbers. These tests prove deterministic classification mechanics — not
/// real-world classification accuracy.
/// </summary>
public class RuleBasedDocumentClassifierTests
{
    private static readonly RuleBasedDocumentClassifier Classifier = new();

    [Fact(DisplayName =
        "Synthetic passport text classifies as Passport")]
    public async Task Passport_Text_ClassifiesPassport()
    {
        var result = await Classifier.ClassifyAsync(
            "PASSPORT\nPASSPORT NUMBER: P-90321\n" +
            "NATIONALITY: TESTLAN\nDATE OF BIRTH: 1990-01-01",
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Synthetic Emirates ID text classifies as EmiratesId")]
    public async Task EmiratesId_Text_ClassifiesEmiratesId()
    {
        var result = await Classifier.ClassifyAsync(
            "EMIRATES ID\nEMIRATES IDENTITY CARD\n" +
            "ID NUMBER: ID-2026-0001\nIDENTITY CARD NUMBER: ID-2026-0001",
            CancellationToken.None);

        Assert.Equal(DocumentType.EmiratesId, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Synthetic visa text classifies as Visa")]
    public async Task Visa_Text_ClassifiesVisa()
    {
        var result = await Classifier.ClassifyAsync(
            "VISA\nRESIDENCE VISA\nVISA NUMBER: V-90321\n" +
            "ENTRY PERMIT: EP-2026-01\nPASSPORT NUMBER: P-90321",
            CancellationToken.None);

        Assert.Equal(DocumentType.Visa, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Synthetic labor card text classifies as LaborCard")]
    public async Task LaborCard_Text_ClassifiesLaborCard()
    {
        var result = await Classifier.ClassifyAsync(
            "LABOR CARD\nLABOUR CARD\n" +
            "LABOR CARD NUMBER: LC-2026-0001\n" +
            "LABOUR CARD NUMBER: LC-2026-0001",
            CancellationToken.None);

        Assert.Equal(DocumentType.LaborCard, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Synthetic trade license text classifies as TradeLicense")]
    public async Task TradeLicense_Text_ClassifiesTradeLicense()
    {
        var result = await Classifier.ClassifyAsync(
            "TRADE LICENSE\nTRADE LICENCE\n" +
            "LICENSE NUMBER: TL-2026-0001\n" +
            "LICENCE NUMBER: TL-2026-0001",
            CancellationToken.None);

        Assert.Equal(DocumentType.TradeLicense, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Synthetic establishment card text classifies as EstablishmentCard")]
    public async Task EstablishmentCard_Text_ClassifiesEstablishmentCard()
    {
        var result = await Classifier.ClassifyAsync(
            "ESTABLISHMENT CARD\nESTABLISHMENT NUMBER: EC-2026-0001\n" +
            "ECONOMIC DEPARTMENT\nCOMPANY NAME: TEST EST LLC",
            CancellationToken.None);

        Assert.Equal(DocumentType.EstablishmentCard, result.Type);
        Assert.True(result.Confidence >= 0.5m);
    }

    [Fact(DisplayName =
        "Unrecognised text returns null/0")]
    public async Task UnknownText_ReturnsNull()
    {
        var result = await Classifier.ClassifyAsync(
            "QUARANTINE CLEARANCE CERTIFICATE\nCASE: 99-102-888",
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Empty text returns null/0")]
    public async Task EmptyText_ReturnsNull()
    {
        var result = await Classifier.ClassifyAsync(
            string.Empty,
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Whitespace-only text returns null/0")]
    public async Task WhitespaceText_ReturnsNull()
    {
        var result = await Classifier.ClassifyAsync(
            "   \t  \n  ",
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Null text returns null/0")]
    public async Task NullText_ReturnsNull()
    {
        var result = await Classifier.ClassifyAsync(
            null,
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "A single strong indicator without supporting indicators is below threshold")]
    public async Task SingleStrong_BelowThreshold_ReturnsNull()
    {
        var result = await Classifier.ClassifyAsync(
            "PASSPORT",
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "One strong plus one supporting indicator is exactly at threshold")]
    public async Task StrongPlusOneSupporting_ExactThreshold()
    {
        var result = await Classifier.ClassifyAsync(
            "PASSPORT NATIONALITY",
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(
            RuleBasedDocumentClassifier.MinimumConfidenceThreshold,
            result.Confidence);
    }

    [Fact(DisplayName =
        "A dominant candidate wins over a weaker runner-up")]
    public async Task StrongCandidate_BeatsWeaker()
    {
        // Visa: "VISA" (3) + "RESIDENCE VISA" (3) + "ENTRY PERMIT" (1) +
        //       "VISA NUMBER" (1) = 8/8 = 1.0
        // Passport: "PASSPORT" (3) + "PASSPORT NUMBER" (3) = 6/8 = 0.75
        var result = await Classifier.ClassifyAsync(
            "VISA\nRESIDENCE VISA\nVISA NUMBER: V-90321\n" +
            "ENTRY PERMIT: EP-2026-01\nPASSPORT NUMBER: P-90321",
            CancellationToken.None);

        Assert.Equal(DocumentType.Visa, result.Type);
        Assert.Equal(1.0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Two equally-scoring types produce a tie and return neutral")]
    public async Task AmbiguousTie_ReturnsNeutral()
    {
        // Trade license: "TRADE LICENSE" (3)
        // Establishment card: "ESTABLISHMENT CARD" (3)
        var result = await Classifier.ClassifyAsync(
            "TRADE LICENSE\nESTABLISHMENT CARD",
            CancellationToken.None);

        Assert.Null(result.Type);
        Assert.Equal(0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Classification is case-insensitive")]
    public async Task CaseInsensitive()
    {
        var result = await Classifier.ClassifyAsync(
            "pAsSpOrT nAtIoNaLiTy",
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(
            RuleBasedDocumentClassifier.MinimumConfidenceThreshold,
            result.Confidence);
    }

    [Fact(DisplayName =
        "Punctuation and multiple whitespace are normalised safely")]
    public async Task WhitespacePunctuationNormalized()
    {
        // "PASSPORT;" → "passport", "NATIONALITY" → "nationality"
        var result = await Classifier.ClassifyAsync(
            "PASSPORT; --- NATIONALITY !!!",
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(
            RuleBasedDocumentClassifier.MinimumConfidenceThreshold,
            result.Confidence);
    }

    [Fact(DisplayName =
        "Cancellation token is propagated")]
    public async Task Cancellation_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Classifier.ClassifyAsync("PASSPORT", cts.Token));
    }

    [Fact(DisplayName =
        "Full passport fixture matches all indicators with maximum confidence")]
    public async Task PassportFullFixture_MaxConfidence()
    {
        var result = await Classifier.ClassifyAsync(
            string.Join("\n", TestDocumentFixtures.PassportLines()),
            CancellationToken.None);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal(1.0m, result.Confidence);
    }

    [Fact(DisplayName =
        "Full trade license fixture matches all indicators")]
    public async Task TradeLicenseFullFixture()
    {
        var result = await Classifier.ClassifyAsync(
            string.Join("\n", TestDocumentFixtures.TradeLicenseLines()),
            CancellationToken.None);

        Assert.Equal(DocumentType.TradeLicense, result.Type);
        Assert.Equal(1.0m, result.Confidence);
    }
}
