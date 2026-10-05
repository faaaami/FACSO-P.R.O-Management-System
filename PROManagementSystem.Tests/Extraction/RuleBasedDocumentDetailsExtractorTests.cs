using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7B.5: deterministic schema-driven field extraction. Rules are tested
/// against the same synthetic, anonymised "LABEL: value" fixtures the
/// classification and Local-provider integration tests use; no real-world
/// document accuracy is claimed.
/// </summary>
public class RuleBasedDocumentDetailsExtractorTests
{
    private static readonly DocumentTypeSchemaProvider Schemas =
        new();

    private static RuleBasedDocumentDetailsExtractor CreateExtractor(
        IDocumentTypeSchemaProvider? provider = null)
        => new(
            provider ?? Schemas,
            new DocumentDetailsValidator(
                provider ?? Schemas));

    private static async Task<IReadOnlyDictionary<string, object?>?> Extract(
        string? text,
        DocumentType? type,
        IDocumentTypeSchemaProvider? provider = null)
    {
        var result = await CreateExtractor(provider).ExtractAsync(
            text,
            type,
            CancellationToken.None);

        return result.Details;
    }

    // ------------------------------------------------------------------
    // Per-type field mapping
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Passport fixture maps every present schema field and ignores unknown labels")]
    public async Task PassportFixture_PopulatesKnownFields()
    {
        var details = await Extract(
            string.Join("\n", TestDocumentFixtures.PassportLines()),
            DocumentType.Passport);

        Assert.NotNull(details);
        Assert.Collection(
            details!.OrderBy(kv => kv.Key, StringComparer.Ordinal),
            kv =>
            {
                Assert.Equal("dateOfBirth", kv.Key);
                Assert.Equal("1990-01-01", kv.Value);
            },
            kv =>
            {
                Assert.Equal("nationality", kv.Key);
                Assert.Equal("TESTLAND", kv.Value);
            },
            kv =>
            {
                Assert.Equal("passportNumber", kv.Key);
                Assert.Equal("P-90321", kv.Value);
            });

        // Close the assertion window for the field-cap mappings above.
        Assert.DoesNotContain(details, kv => kv.Key == "surname");
        Assert.DoesNotContain(details, kv => kv.Key == "placeOfBirth");
        Assert.DoesNotContain(details, kv => kv.Key == "sex");
        Assert.DoesNotContain(details, kv => kv.Key == "issueDate");
        Assert.DoesNotContain(details, kv => kv.Key == "expiryDate");
        Assert.DoesNotContain(details, kv => kv.Key == "issuingAuthority");
    }

    [Theory(DisplayName =
        "Each document type maps its primary identifier label to its schema key")]
    [InlineData(DocumentType.Visa, "VISA NUMBER: V-90321", "visaNumber", "V-90321")]
    [InlineData(DocumentType.EmiratesId, "ID NUMBER: ID-2026-0001", "idNumber", "ID-2026-0001")]
    [InlineData(DocumentType.LaborCard, "LABOR CARD NUMBER: LC-2026-0001", "laborCardNumber", "LC-2026-0001")]
    [InlineData(DocumentType.TradeLicense, "LICENSE NUMBER: TL-2026-0001", "licenseNumber", "TL-2026-0001")]
    [InlineData(DocumentType.EstablishmentCard, "ESTABLISHMENT NUMBER: EC-2026-0001", "establishmentNumber", "EC-2026-0001")]
    public async Task PrimaryIdentifiers_MapToSchemaKeys(
        DocumentType type,
        string line,
        string expectedKey,
        string expectedValue)
    {
        var details = await Extract(line, type);

        Assert.NotNull(details);
        Assert.Equal(expectedValue, details![expectedKey]);
    }

    [Fact(DisplayName =
        "Synonym labels resolve to the same schema key (labour/labor, licence/license, id/identity card)")]
    public async Task SynonymLabels_ResolveToSameKey()
    {
        var labour = await Extract(
            "LABOUR CARD NUMBER: LC-2026-0001",
            DocumentType.LaborCard);
        var licence = await Extract(
            "LICENCE NUMBER: TL-2026-0001",
            DocumentType.TradeLicense);
        var identityCard = await Extract(
            "IDENTITY CARD NUMBER: ID-2026-0001",
            DocumentType.EmiratesId);

        Assert.Equal("LC-2026-0001", labour!["laborCardNumber"]);
        Assert.Equal("TL-2026-0001", licence!["licenseNumber"]);
        Assert.Equal("ID-2026-0001", identityCard!["idNumber"]);
    }

    [Fact(DisplayName =
        "Company name and economic department are extracted for EstablishmentCard")]
    public async Task EstablishmentCard_PopulatesCompanyFields()
    {
        var details = await Extract(
            "ESTABLISHMENT NUMBER: EC-2026-0001\n" +
            "COMPANY NAME: TEST ESTABLISHMENT LLC\n" +
            "ECONOMIC DEPARTMENT",
            DocumentType.EstablishmentCard);

        Assert.NotNull(details);
        Assert.Equal("EC-2026-0001", details!["establishmentNumber"]);
        Assert.Equal("TEST ESTABLISHMENT LLC", details["companyName"]);

        // "ECONOMIC DEPARTMENT" carries no value, so it stays absent.
        Assert.DoesNotContain(details, kv => kv.Key == "economicDepartment");
    }

    // ------------------------------------------------------------------
    // Label matching
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Label matching is case-insensitive")]
    public async Task LabelMatching_IsCaseInsensitive()
    {
        var details = await Extract(
            "Passport Number: P-90321\nNationality: TESTLAND",
            DocumentType.Passport);

        Assert.Equal("P-90321", details!["passportNumber"]);
        Assert.Equal("TESTLAND", details["nationality"]);
    }

    [Fact(DisplayName =
        "Label matching tolerates collapsed internal whitespace")]
    public async Task LabelMatching_CollapsesLabelWhitespace()
    {
        var details = await Extract(
            "PASSPORT   NUMBER: P-90321",
            DocumentType.Passport);

        Assert.Equal("P-90321", details!["passportNumber"]);
    }

    // ------------------------------------------------------------------
    // Dates
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Strict yyyy-MM-dd dates are accepted and kept canonical")]
    public async Task Dates_AcceptStrictIso()
    {
        var details = await Extract(
            "ISSUE DATE: 2021-04-10\nEXPIRY DATE: 2031-04-10\n" +
            "DATE OF BIRTH: 2001-04-10",
            DocumentType.Passport);

        Assert.Equal("2021-04-10", details!["issueDate"]);
        Assert.Equal("2031-04-10", details["expiryDate"]);
        Assert.Equal("2001-04-10", details["dateOfBirth"]);
    }

    [Theory(DisplayName =
        "Malformed dates are dropped, valid fields are retained")]
    [InlineData("1990/01/01")]
    [InlineData("01-01-1990")]
    [InlineData("not-a-date")]
    [InlineData("1990-1-1")]
    public async Task Dates_InvalidCandidatesAreDropped(string badDate)
    {
        var details = await Extract(
            $"PASSPORT NUMBER: P-90321\nDATE OF BIRTH: {badDate}",
            DocumentType.Passport);

        Assert.NotNull(details);
        Assert.DoesNotContain(details, kv => kv.Key == "dateOfBirth");
        Assert.Equal("P-90321", details!["passportNumber"]);
    }

    // ------------------------------------------------------------------
    // MaxLength and string boundaries
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An over-length identifier is dropped rather than truncated")]
    public async Task OverLengthValue_IsDropped()
    {
        // 51 characters: one past the passport number's 50-character cap.
        var tooLong = "P-" + new string('1', 49);
        var details = await Extract(
            $"PASSPORT NUMBER: {tooLong}",
            DocumentType.Passport);

        Assert.NotNull(details);
        Assert.Empty(details);
    }

    [Fact(DisplayName =
        "A value at exactly MaxLength is retained")]
    public async Task BoundaryLength_IsRetained()
    {
        // Exactly 50 characters: the passport number cap.
        var atLimit = "P-" + new string('1', 48);
        var details = await Extract(
            $"PASSPORT NUMBER: {atLimit}",
            DocumentType.Passport);

        Assert.Equal(atLimit, details!["passportNumber"]);
    }

    // ------------------------------------------------------------------
    // Partial extraction
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Partial extraction keeps only the fields that are present")]
    public async Task PartialExtraction_KeepsPresentFields()
    {
        var details = await Extract(
            "PASSPORT NUMBER: P-1234567\nNATIONALITY: INDIAN",
            DocumentType.Passport);

        Assert.Equal("P-1234567", details!["passportNumber"]);
        Assert.Equal("INDIAN", details["nationality"]);

        Assert.DoesNotContain(details, kv => kv.Key == "dateOfBirth");
        Assert.DoesNotContain(details, kv => kv.Key == "placeOfBirth");
        Assert.DoesNotContain(details, kv => kv.Key == "sex");
        Assert.DoesNotContain(details, kv => kv.Key == "issueDate");
        Assert.DoesNotContain(details, kv => kv.Key == "expiryDate");
        Assert.DoesNotContain(details, kv => kv.Key == "issuingAuthority");
    }

    // ------------------------------------------------------------------
    // Empty / unknown / Other
    // ------------------------------------------------------------------

    [Theory(DisplayName =
        "Empty, whitespace-only and null text produce no details")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \n\t  ")]
    public async Task EmptyText_ProducesNoDetails(string? text)
    {
        var details = await Extract(text, DocumentType.Passport);

        Assert.NotNull(details);
        Assert.Empty(details);
    }

    [Fact(DisplayName =
        "A null (unclassified) suggested type produces no details")]
    public async Task NullType_ProducesNoDetails()
    {
        var details = await Extract("PASSPORT NUMBER: P-90321", null);

        Assert.NotNull(details);
        Assert.Empty(details);
    }

    [Fact(DisplayName =
        "Other resolves to the empty schema and produces no details")]
    public async Task Other_ProducesNoDetails()
    {
        var details = await Extract(
            "ESTABLISHMENT NUMBER: EC-2026-0001",
            DocumentType.Other);

        Assert.NotNull(details);
        Assert.Empty(details);
    }

    [Fact(DisplayName =
        "Unknown document text leaves every field unextracted even when text matches a label")]
    public async Task UnknownDocumentText_ProducesNoDetails()
    {
        var details = await Extract(
            "QUARANTINE CLEARANCE CERTIFICATE\n" +
            "CASE NUMBER: 99-102-888\nRESULT: CLEARED",
            DocumentType.EstablishmentCard);

        Assert.NotNull(details);
        Assert.Empty(details);
    }

    [Fact(DisplayName =
        "Unknown labels are ignored and never invented into keys")]
    public async Task UnknownLabels_AreIgnored()
    {
        var details = await Extract(
            "PASSPORT NUMBER: P-90321\n" +
            "SURNAME: FAKEHOLDER\n" +
            "GIVEN NAMES: FAKE HOLDER",
            DocumentType.Passport);

        Assert.NotNull(details);
        Assert.Single(details);
        Assert.Equal("P-90321", details!["passportNumber"]);
    }

    // ------------------------------------------------------------------
    // Normalization
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Values are trimmed and interior whitespace collapses to one space")]
    public async Task Values_TrimAndCollapseWhitespace()
    {
        var details = await Extract(
            "   COMPANY NAME:    TEST   ESTABLISHMENT    LLC   ",
            DocumentType.EstablishmentCard);

        Assert.Equal("TEST ESTABLISHMENT LLC", details!["companyName"]);
    }

    [Fact(DisplayName =
        "Trailing punctuation is stripped from string values")]
    public async Task Values_StripTrailingPunctuation()
    {
        var details = await Extract(
            "PASSPORT NUMBER: P-90321.",
            DocumentType.Passport);

        Assert.Equal("P-90321", details!["passportNumber"]);
    }

    [Fact(DisplayName =
        "Value casing and Unicode are preserved")]
    public async Task Values_PreserveCasingAndUnicode()
    {
        var latin = await Extract(
            "NATIONALITY: Indian",
            DocumentType.Passport);
        var unicode = await Extract(
            "NATIONALITY: \u0100M\u012A",
            DocumentType.Passport);

        Assert.Equal("Indian", latin!["nationality"]);
        Assert.Equal("\u0100M\u012A", unicode!["nationality"]);
    }

    // ------------------------------------------------------------------
    // Generic value-kind handling for future schema additions
    // ------------------------------------------------------------------

    /// <summary>
    /// Reuses an existing schema label with a future Number/Boolean field so
    /// the generic value-kind switch is exercised without touching the real
    /// registry (which currently holds only String/Date fields).
    /// </summary>
    private sealed class NumberAndBooleanProvider
        : IDocumentTypeSchemaProvider
    {
        private static readonly DocumentFieldDefinition[] Fields =
        {
            new(
                "economicDepartment",
                "Economic Department",
                DocumentFieldValueKind.Number),
            new(
                "cardNumber",
                "Card Number",
                DocumentFieldValueKind.Boolean),
        };

        public IReadOnlyList<DocumentFieldDefinition> Get(
            DocumentType documentType)
            => documentType == DocumentType.Passport ? Fields : Array.Empty<DocumentFieldDefinition>();
    }

    [Fact(DisplayName =
        "A future Number field parses to a JSON number")]
    public async Task NumberKind_ReturnsDecimal()
    {
        var details = await Extract(
            "ECONOMIC DEPARTMENT: 1234.5",
            DocumentType.Passport,
            new NumberAndBooleanProvider());

        Assert.Equal(1234.5m, Assert.IsType<decimal>(details!["economicDepartment"]));
    }

    [Fact(DisplayName =
        "A future Boolean field parses to true/false")]
    public async Task BooleanKind_ReturnsBoolean()
    {
        var trueVal = await Extract(
            "CARD NUMBER: true",
            DocumentType.Passport,
            new NumberAndBooleanProvider());
        var falseVal = await Extract(
            "CARD NUMBER: false",
            DocumentType.Passport,
            new NumberAndBooleanProvider());
        var garbage = await Extract(
            "CARD NUMBER: maybe",
            DocumentType.Passport,
            new NumberAndBooleanProvider());

        Assert.True(Assert.IsType<bool>(trueVal!["cardNumber"]));
        Assert.False(Assert.IsType<bool>(falseVal!["cardNumber"]));
        Assert.Empty(garbage!);
    }

    [Fact(DisplayName =
        "A malformed Number candidate is dropped")]
    public async Task NumberKind_InvalidCandidateIsDropped()
    {
        var details = await Extract(
            "ECONOMIC DEPARTMENT: 12a4",
            DocumentType.Passport,
            new NumberAndBooleanProvider());

        Assert.Empty(details!);
    }
}