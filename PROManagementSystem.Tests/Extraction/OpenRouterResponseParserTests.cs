using System.Net;
using System.Reflection;
using System.Text.Json;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Infrastructure.AI.DocumentExtractionService;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.3.2: the structured OpenRouter response parser. Pure unit tests —
/// synthetic OpenRouter JSON only, no database and no network. They prove the
/// choices/message/content envelope handling, the logical-object mapping,
/// schema-restricted details, per-value-kind validation, confidence
/// combination, and the fail-soft/security guarantees (raw response never
/// exposed, parser has no logging surface).
/// </summary>
public class OpenRouterResponseParserTests
{
    private static readonly DocumentTypeSchemaProvider Schemas = new();

    private static readonly OpenRouterResponseParser Parser =
        new(Schemas, new DocumentDetailsValidator(Schemas));

    // ------------------------------------------------------------------
    // Valid logical objects per type
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A valid Passport response maps every field and schema detail")]
    public void Passport_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "documentNumber": "P123456",
              "issueDate": "2025-01-01",
              "expiryDate": "2035-01-01",
              "ownerType": "Employee",
              "details": {
                "passportNumber": "P123456",
                "nationality": "Indian"
              },
              "confidence": 0.94
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal("P123456", result.DocumentNumber);
        Assert.Equal(new DateTime(2025, 1, 1), result.IssueDate);
        Assert.Equal(new DateTime(2035, 1, 1), result.ExpiryDate);
        Assert.Equal("Employee", result.OwnerType);
        Assert.Equal(0.94m, result.Confidence);
        Assert.Equal("P123456", result.Details!["passportNumber"]);
        Assert.Equal("Indian", result.Details!["nationality"]);
    }

    [Fact(DisplayName = "A valid Visa response maps correctly")]
    public void Visa_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Visa",
              "details": {
                "visaNumber": "V-90321",
                "visaType": "Residence"
              },
              "confidence": 0.8
            }
            """);

        Assert.Equal(DocumentType.Visa, result.Type);
        Assert.Equal("V-90321", result.Details!["visaNumber"]);
        Assert.Equal("Residence", result.Details!["visaType"]);
    }

    [Fact(DisplayName = "A valid EmiratesId response maps correctly")]
    public void EmiratesId_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "EmiratesId",
              "details": {
                "idNumber": "784-1990-1234567-6",
                "cardNumber": "CARD-1",
                "nationality": "Testland"
              }
            }
            """);

        Assert.Equal(DocumentType.EmiratesId, result.Type);
        Assert.Equal("784-1990-1234567-6", result.Details!["idNumber"]);
        Assert.Equal("CARD-1", result.Details!["cardNumber"]);
        Assert.Equal("Testland", result.Details!["nationality"]);
    }

    [Fact(DisplayName = "A valid LaborCard response maps correctly")]
    public void LaborCard_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "LaborCard",
              "details": {
                "laborCardNumber": "LC-2026-0001",
                "occupation": "Engineer"
              }
            }
            """);

        Assert.Equal(DocumentType.LaborCard, result.Type);
        Assert.Equal("LC-2026-0001", result.Details!["laborCardNumber"]);
        Assert.Equal("Engineer", result.Details!["occupation"]);
    }

    [Fact(DisplayName = "A valid TradeLicense response maps correctly")]
    public void TradeLicense_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "TradeLicense",
              "details": {
                "licenseNumber": "TL-2026-0001",
                "legalName": "Test LLC"
              }
            }
            """);

        Assert.Equal(DocumentType.TradeLicense, result.Type);
        Assert.Equal("TL-2026-0001", result.Details!["licenseNumber"]);
        Assert.Equal("Test LLC", result.Details!["legalName"]);
    }

    [Fact(DisplayName = "A valid EstablishmentCard response maps correctly")]
    public void EstablishmentCard_MapsCorrectly()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "EstablishmentCard",
              "details": {
                "establishmentNumber": "EC-2026-0001",
                "companyName": "Test Establishment LLC"
              }
            }
            """);

        Assert.Equal(DocumentType.EstablishmentCard, result.Type);
        Assert.Equal(
            "EC-2026-0001",
            result.Details!["establishmentNumber"]);
        Assert.Equal(
            "Test Establishment LLC",
            result.Details!["companyName"]);
    }

    // ------------------------------------------------------------------
    // Envelope handling
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "The normal chat/completions wrapper is parsed")]
    public void NormalWrapper_IsParsed()
    {
        var result = ParseRaw(
            Envelope(
                """
                { "documentType": "Passport", "documentNumber": "P1" }
                """));

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal("P1", result.DocumentNumber);
    }

    [Fact(DisplayName =
        "The model JSON content is the source of the extraction object")]
    public void ModelContent_IsParsed()
    {
        // Extra envelope properties must never be mistaken for extraction data.
        var raw = "{\"id\":\"gen-1\",\"model\":\"test-model\"," +
                  "\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\"," +
                  "\"content\":" +
                  JsonSerializer.Serialize(
                      "{\"documentType\":\"Visa\",\"documentNumber\":\"V1\"}") +
                  "}}]}";

        var result = ParseRaw(raw);

        Assert.Equal(DocumentType.Visa, result.Type);
        Assert.Equal("V1", result.DocumentNumber);
    }

    [Fact(DisplayName =
        "Property and detail names are matched case-insensitively")]
    public void PropertyNames_AreCaseInsensitive()
    {
        var result = ParseLogical(
            """
            {
              "DocumentType": "passport",
              "DOCUMENTNUMBER": "  P9  ",
              "Details": { "Nationality": "Testland" }
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal("P9", result.DocumentNumber);
        Assert.Equal("Testland", result.Details!["nationality"]);
    }

    // ------------------------------------------------------------------
    // Unknown / missing / malformed → neutral
    // ------------------------------------------------------------------

    [Fact(DisplayName = "An unknown document type becomes neutral")]
    public void UnknownDocumentType_IsNeutral()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Spaceship",
              "documentNumber": "X1",
              "details": { "passportNumber": "X1" },
              "confidence": 0.9
            }
            """);

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A missing documentType becomes neutral")]
    public void MissingDocumentType_IsNeutral()
    {
        var result = ParseLogical(
            """
            { "documentNumber": "P1", "confidence": 0.9 }
            """);

        AssertNeutral(result);
    }

    [Fact(DisplayName = "Malformed outer JSON becomes neutral")]
    public void MalformedOuterJson_IsNeutral()
    {
        var result = ParseRaw("{ this is not json");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "Malformed model JSON becomes neutral")]
    public void MalformedModelJson_IsNeutral()
    {
        var result = ParseRaw(Envelope("{ this is not json"));

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A response with no choices becomes neutral")]
    public void MissingChoices_IsNeutral()
    {
        var result = ParseRaw("{\"id\":\"gen-1\"}");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A response with an empty choices array becomes neutral")]
    public void EmptyChoices_IsNeutral()
    {
        var result = ParseRaw("{\"choices\":[]}");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A response without a message becomes neutral")]
    public void MissingMessage_IsNeutral()
    {
        var result = ParseRaw(
            "{\"choices\":[{\"index\":0,\"finish_reason\":\"stop\"}]}");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A response without content becomes neutral")]
    public void MissingContent_IsNeutral()
    {
        var result = ParseRaw(
            "{\"choices\":[{\"message\":{\"role\":\"assistant\"}}]}");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A null content becomes neutral")]
    public void NullContent_IsNeutral()
    {
        var result = ParseRaw(
            "{\"choices\":[{\"message\":{\"content\":null}}]}");

        AssertNeutral(result);
    }

    [Fact(DisplayName = "A non-string content becomes neutral")]
    public void NonStringContent_IsNeutral()
    {
        var result = ParseRaw(
            "{\"choices\":[{\"message\":{\"content\":123}}]}");

        AssertNeutral(result);
    }

    [Fact(DisplayName =
        "A logical JSON array instead of an object becomes neutral")]
    public void LogicalArray_IsNeutral()
    {
        var result = ParseLogical("[\"Passport\",\"P1\"]");

        AssertNeutral(result);
    }

    [Fact(DisplayName =
        "An over-length document number is dropped while valid fields survive")]
    public void OverLengthDocumentNumber_IsDropped()
    {
        var tooLong = new string('D', 201);

        var result = ParseLogical(
            $$"""
            {
              "documentType": "Passport",
              "documentNumber": "{{tooLong}}",
              "ownerType": "Employee",
              "details": { "passportNumber": "P1" }
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Null(result.DocumentNumber);
        Assert.Equal("Employee", result.OwnerType);
        Assert.Equal("P1", result.Details!["passportNumber"]);
    }

    [Fact(DisplayName =
        "An over-length owner type is dropped while valid fields survive")]
    public void OverLengthOwnerType_IsDropped()
    {
        var tooLong = new string('O', 51);

        var result = ParseLogical(
            $$"""
            {
              "documentType": "Passport",
              "documentNumber": "P123456",
              "ownerType": "{{tooLong}}",
              "details": { "passportNumber": "P123456" }
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Null(result.OwnerType);
        Assert.Equal("P123456", result.DocumentNumber);
        Assert.Equal("P123456", result.Details!["passportNumber"]);
    }

    [Fact(DisplayName =
        "A non-object details value is dropped without losing the type")]
    public void NonObjectDetails_AreDropped()
    {
        var result = ParseLogical(
            "{\"documentType\":\"Passport\"," +
            "\"details\":[\"passportNumber\"]}");

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Null(result.Details);
    }

    [Fact(DisplayName = "An invalid issue date format is dropped")]
    public void InvalidIssueDate_IsDropped()
    {
        var result = ParseLogical(
            "{\"documentType\":\"Passport\"," +
            "\"issueDate\":\"01/01/2025\"," +
            "\"expiryDate\":\"2035-01-01\"}");

        Assert.Null(result.IssueDate);
        Assert.Equal(new DateTime(2035, 1, 1), result.ExpiryDate);
    }

    [Fact(DisplayName =
        "One invalid field does not destroy unrelated valid fields")]
    public void OneBadField_DoesNotDestroyValidFields()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "documentNumber": "P-VALID",
              "issueDate": "not-a-date",
              "ownerType": "Employee",
              "details": {
                "passportNumber": "P-VALID",
                "nationality": 123
              }
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.Equal("P-VALID", result.DocumentNumber);
        Assert.Null(result.IssueDate);
        Assert.Equal("Employee", result.OwnerType);
        Assert.Equal("P-VALID", result.Details!["passportNumber"]);
        Assert.False(result.Details!.ContainsKey("nationality"));
    }

    // ------------------------------------------------------------------
    // Schema filtering and value validation (real schema)
    // ------------------------------------------------------------------

    [Fact(DisplayName = "Unknown detail fields are removed")]
    public void UnknownDetailFields_AreRemoved()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": {
                "passportNumber": "P1",
                "unknownField": "ignored",
                "anotherUnknown": 123
              }
            }
            """);

        Assert.Equal("P1", result.Details!["passportNumber"]);
        Assert.False(result.Details!.ContainsKey("unknownField"));
        Assert.False(result.Details!.ContainsKey("anotherUnknown"));
    }

    [Fact(DisplayName = "Valid schema fields remain")]
    public void ValidSchemaFields_Remain()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": {
                "passportNumber": "P1",
                "nationality": "Testland",
                "dateOfBirth": "1990-01-01"
              }
            }
            """);

        Assert.Equal(3, result.Details!.Count);
        Assert.Equal("1990-01-01", result.Details["dateOfBirth"]);
    }

    [Fact(DisplayName =
        "A string exceeding the schema MaxLength is dropped, not truncated")]
    public void StringMaxLength_IsEnforced()
    {
        var tooLong = new string('A', 51);

        var result = ParseLogical(
            $$"""
            {
              "documentType": "Passport",
              "details": {
                "passportNumber": "{{tooLong}}",
                "nationality": "Testland"
              }
            }
            """);

        Assert.False(result.Details!.ContainsKey("passportNumber"));
        Assert.Equal("Testland", result.Details["nationality"]);
    }

    [Fact(DisplayName = "An invalid date detail is removed")]
    public void InvalidDateDetail_IsRemoved()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": {
                "passportNumber": "P1",
                "dateOfBirth": "01/01/1990"
              }
            }
            """);

        Assert.False(result.Details!.ContainsKey("dateOfBirth"));
        Assert.Equal("P1", result.Details["passportNumber"]);
    }

    [Fact(DisplayName = "A valid date detail is preserved verbatim")]
    public void ValidDateDetail_IsPreserved()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": { "dateOfBirth": "1990-12-31" }
            }
            """);

        Assert.Equal("1990-12-31", result.Details!["dateOfBirth"]);
    }

    [Fact(DisplayName = "An invalid number detail is removed")]
    public void InvalidNumberDetail_IsRemoved()
    {
        var result = ParseWithStubSchema(
            """
            {
              "documentType": "TradeLicense",
              "details": { "count": "not-a-number", "flag": true }
            }
            """);

        Assert.False(result.Details!.ContainsKey("count"));
        Assert.Equal(true, result.Details["flag"]);
    }

    [Fact(DisplayName = "An invalid boolean detail is removed")]
    public void InvalidBooleanDetail_IsRemoved()
    {
        var result = ParseWithStubSchema(
            """
            {
              "documentType": "TradeLicense",
              "details": { "flag": "yes", "count": 5 }
            }
            """);

        Assert.False(result.Details!.ContainsKey("flag"));
        Assert.Equal(5m, result.Details["count"]);
    }

    [Fact(DisplayName = "Valid number and boolean details use native types")]
    public void ValidPrimitiveDetails_UseNativeTypes()
    {
        var result = ParseWithStubSchema(
            """
            {
              "documentType": "TradeLicense",
              "details": { "count": 42, "flag": false }
            }
            """);

        Assert.Equal(42m, result.Details!["count"]);
        Assert.Equal(false, result.Details["flag"]);
    }

    // ------------------------------------------------------------------
    // Confidence combination
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An invalid model confidence falls back to the classifier confidence")]
    public void InvalidConfidence_FallsBack()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "confidence": "high" }
            """,
            classificationConfidence: 0.42m);

        Assert.Equal(0.42m, result.Confidence);
    }

    [Fact(DisplayName =
        "A model confidence above 1 is rejected and falls back")]
    public void ConfidenceAboveOne_FallsBack()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "confidence": 1.5 }
            """,
            classificationConfidence: 0.25m);

        Assert.Equal(0.25m, result.Confidence);
    }

    [Fact(DisplayName =
        "A negative model confidence is rejected and falls back")]
    public void NegativeConfidence_FallsBack()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "confidence": -0.2 }
            """,
            classificationConfidence: 0.25m);

        Assert.Equal(0.25m, result.Confidence);
    }

    [Fact(DisplayName =
        "A missing model confidence retains the classifier confidence")]
    public void MissingConfidence_RetainsClassifierConfidence()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport" }
            """,
            classificationConfidence: 0.7m);

        Assert.Equal(0.7m, result.Confidence);
    }

    [Fact(DisplayName =
        "An invalid classifier fallback is itself validated to zero")]
    public void InvalidClassifierFallback_BecomesZero()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "confidence": 2 }
            """,
            classificationConfidence: -1m);

        Assert.Equal(0m, result.Confidence);
    }

    // ------------------------------------------------------------------
    // Top-level field behavior
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A missing required detail does not fabricate a value")]
    public void MissingRequiredField_DoesNotFabricate()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": { "nationality": "Testland" }
            }
            """);

        Assert.Equal(DocumentType.Passport, result.Type);
        Assert.False(result.Details!.ContainsKey("passportNumber"));
        Assert.Equal("Testland", result.Details["nationality"]);
    }

    [Fact(DisplayName =
        "Details remain restricted to schema keys only")]
    public void Details_RemainSchemaRestricted()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "details": {
                "passportNumber": "P1",
                "totallyUnknown": "x"
              }
            }
            """);

        var schemaKeys = Schemas.Get(DocumentType.Passport)
            .Select(f => f.Key)
            .ToHashSet(StringComparer.Ordinal);

        Assert.All(
            result.Details!.Keys,
            key => Assert.Contains(key, schemaKeys));
    }

    [Fact(DisplayName =
        "An issue date after the expiry date is dropped without correction")]
    public void IssueAfterExpiry_DropsBothDates()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Passport",
              "issueDate": "2030-01-01",
              "expiryDate": "2025-01-01"
            }
            """);

        Assert.Null(result.IssueDate);
        Assert.Null(result.ExpiryDate);
    }

    [Fact(DisplayName = "A whitespace document number is trimmed")]
    public void DocumentNumber_IsTrimmed()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "documentNumber": "   P123   " }
            """);

        Assert.Equal("P123", result.DocumentNumber);
    }

    [Fact(DisplayName = "A whitespace owner type is trimmed")]
    public void OwnerType_IsTrimmed()
    {
        var result = ParseLogical(
            """
            { "documentType": "Passport", "ownerType": "   Employee   " }
            """);

        Assert.Equal("Employee", result.OwnerType);
    }

    [Fact(DisplayName =
        "Other produces no invented schema fields")]
    public void OtherType_ProducesNoDetails()
    {
        var result = ParseLogical(
            """
            {
              "documentType": "Other",
              "details": { "passportNumber": "P1" }
            }
            """);

        Assert.Equal(DocumentType.Other, result.Type);
        Assert.Null(result.Details);
    }

    // ------------------------------------------------------------------
    // Security guarantees
    // ------------------------------------------------------------------

    [Fact(DisplayName = "The parser has no logging dependency")]
    public void Parser_HasNoLoggingDependency()
    {
        var constructor = Assert.Single(
            typeof(OpenRouterResponseParser).GetConstructors(
                BindingFlags.Instance
                | BindingFlags.Public
                | BindingFlags.NonPublic));

        Assert.DoesNotContain(
            constructor.GetParameters(),
            parameter => parameter.ParameterType.Name.Contains(
                "Logger",
                StringComparison.OrdinalIgnoreCase));
    }

    [Fact(DisplayName =
        "The raw model JSON is never exposed through the result")]
    public void RawJson_IsNeverExposed()
    {
        const string marker = "RAW-JSON-MARKER-7C32";

        var result = ParseLogical(
            $$"""
            {
              "documentType": "Passport",
              "documentNumber": "P1",
              "ownerType": "Employee",
              "unknownTopLevel": "{{marker}}",
              "details": {
                "passportNumber": "P1",
                "leakedUnknown": "{{marker}}"
              }
            }
            """);

        Assert.DoesNotContain(marker, result.DocumentNumber ?? string.Empty);
        Assert.DoesNotContain(marker, result.OwnerType ?? string.Empty);
        Assert.False(result.Details!.ContainsKey("unknownTopLevel"));
        Assert.False(result.Details.ContainsKey("leakedUnknown"));
        Assert.All(
            result.Details.Values,
            value => Assert.NotEqual(marker, value?.ToString()));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static void AssertNeutral(AiParsedExtraction result)
    {
        Assert.Null(result.Type);
        Assert.Null(result.DocumentNumber);
        Assert.Null(result.IssueDate);
        Assert.Null(result.ExpiryDate);
        Assert.Null(result.OwnerType);
        Assert.Equal(0m, result.Confidence);
        Assert.Null(result.Details);
    }

    private static string Envelope(string logicalJson)
        => "{\"id\":\"gen-1\",\"choices\":[{\"message\":{\"content\":" +
           JsonSerializer.Serialize(logicalJson) +
           "}}]}";

    private static OpenRouterRawResponse Raw(string json)
        => new(HttpStatusCode.OK, true, json);

    private static AiParsedExtraction ParseRaw(string json)
        => Parser.Parse(Raw(json), null, 0m);

    private static AiParsedExtraction ParseLogical(
        string logicalJson,
        decimal classificationConfidence = 0m)
        => Parser.Parse(
            Raw(Envelope(logicalJson)),
            null,
            classificationConfidence);

    /// <summary>
    /// Parses with a schema that includes Number and Boolean fields, because
    /// the production registry currently exposes only String and Date. This
    /// proves the Number/Boolean conversion branches without touching the real
    /// registry.
    /// </summary>
    private static AiParsedExtraction ParseWithStubSchema(
        string logicalJson)
    {
        IDocumentTypeSchemaProvider schemas = new StubPrimitiveSchemaProvider();
        var parser = new OpenRouterResponseParser(
            schemas,
            new DocumentDetailsValidator(schemas));

        return parser.Parse(
            Raw(Envelope(logicalJson)),
            null,
            0m);
    }

    private sealed class StubPrimitiveSchemaProvider
        : IDocumentTypeSchemaProvider
    {
        private static readonly IReadOnlyList<DocumentFieldDefinition> Fields =
            new[]
            {
                new DocumentFieldDefinition(
                    "count",
                    "Count",
                    DocumentFieldValueKind.Number),
                new DocumentFieldDefinition(
                    "flag",
                    "Flag",
                    DocumentFieldValueKind.Boolean),
                new DocumentFieldDefinition(
                    "code",
                    "Code",
                    DocumentFieldValueKind.String,
                    MaxLength: 5),
            };

        public IReadOnlyList<DocumentFieldDefinition> Get(
            DocumentType documentType)
            => documentType == DocumentType.TradeLicense
                ? Fields
                : Array.Empty<DocumentFieldDefinition>();
    }
}
