using System.Text.Json;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Application.Common.Validation;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="DocumentDetailsValidator"/> (Task 7B.5). These
/// cover the type-specific schema rules in isolation from the API pipeline:
/// value-kind checks (String/Date/Number/Boolean) and MaxLength apply to every
/// known key, Required presence applies only when validateRequired is true,
/// unknown keys and empty Details are never constrained, and DocumentType.Other
/// / unknown enum values are exempt. They do not touch persistence or HTTP.
/// </summary>
public class DocumentDetailsValidatorTests
{
    private static readonly IDocumentTypeSchemaProvider Provider =
        new DocumentTypeSchemaProvider();

    private static readonly DocumentDetailsValidator Validator =
        new(Provider);

    // An unconstrained-schema provider used to exercise Number/Boolean and
    // MaxLength rules that the real Passport schema does not expose.
    private sealed class AllKindsSchemaProvider
        : IDocumentTypeSchemaProvider
    {
        public IReadOnlyList<DocumentFieldDefinition> Get(
            DocumentType documentType)
            => new[]
            {
                new DocumentFieldDefinition(
                    "stringField",
                    "String Field",
                    DocumentFieldValueKind.String),
                new DocumentFieldDefinition(
                    "numberField",
                    "Number Field",
                    DocumentFieldValueKind.Number),
                new DocumentFieldDefinition(
                    "booleanField",
                    "Boolean Field",
                    DocumentFieldValueKind.Boolean),
                new DocumentFieldDefinition(
                    "dateField",
                    "Date Field",
                    DocumentFieldValueKind.Date),
                new DocumentFieldDefinition(
                    "valueField",
                    "Value Field",
                    DocumentFieldValueKind.String,
                    MaxLength: 5),
                new DocumentFieldDefinition(
                    "requiredField",
                    "Required Field",
                    DocumentFieldValueKind.String,
                    Required: true),
            };
    }

    private static readonly DocumentDetailsValidator AllKindsValidator =
        new(new AllKindsSchemaProvider());

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static JsonElement Obj(
        params (string Key, object? Value)[] entries)
    {
        var dictionary = new Dictionary<string, object?>();

        foreach (var (key, value) in entries)
        {
            dictionary[key] = value;
        }

        return JsonSerializer.SerializeToElement(dictionary);
    }

    private static string? ErrorFor(
        IReadOnlyList<FluentValidation.Results.ValidationFailure> failures,
        string propertyName)
        => failures
            .FirstOrDefault(x => x.PropertyName == propertyName)
            ?.ErrorMessage;

    // ------------------------------------------------------------------
    // Required presence (confirmation only)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Confirming a Passport with the required passportNumber passes")]
    public void Required_PresentOnConfirm_Passes()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1")),
            validateRequired: true);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "Confirming a Passport without passportNumber is a required failure")]
    public void Required_MissingOnConfirm_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("nationality", "Indian")),
            validateRequired: true);

        var failure = Assert.Single(failures);
        Assert.Equal("Details.passportNumber", failure.PropertyName);
        Assert.Contains("required", failure.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName =
        "Required is not enforced when validateRequired is false")]
    public void Required_MissingWithoutConfirmFlag_Passes()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("nationality", "Indian")),
            validateRequired: false);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "Confirming a Passport with null Details is a required failure")]
    public void Required_NullDetailsOnConfirm_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            null,
            validateRequired: true);

        Assert.Equal(
            "Details.passportNumber",
            Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "Confirming a Passport with the empty object is a required failure")]
    public void Required_EmptyObjectOnConfirm_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(),
            validateRequired: true);

        Assert.Equal(
            "Details.passportNumber",
            Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "A JSON-null passportNumber counts as missing on confirmation")]
    public void Required_JsonNullOnConfirm_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", (object?)null)),
            validateRequired: true);

        Assert.Equal(
            "Details.passportNumber",
            Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "A JSON-null passportNumber is ignored outside confirmation")]
    public void Required_JsonNullOnCreate_Passes()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", (object?)null)),
            validateRequired: false);

        Assert.Empty(failures);
    }

    // ------------------------------------------------------------------
    // String / Date value kinds (real Passport schema)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A non-string nationality is a type failure")]
    public void StringField_NonStringValue_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("nationality", 123)),
            validateRequired: false);

        Assert.Equal(
            "Details.nationality",
            Assert.Single(failures).PropertyName);
        Assert.Contains("text", Assert.Single(failures).ErrorMessage);
    }

    [Fact(DisplayName =
        "A boolean nationality is a type failure")]
    public void StringField_BooleanValue_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("nationality", true)),
            validateRequired: false);

        Assert.Equal(
            "Details.nationality",
            Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "An ISO date-only dateOfBirth passes")]
    public void DateField_ValidIsoDate_Passes()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("dateOfBirth", "2001-04-10")),
            validateRequired: false);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "A dateOfBirth with a time component fails the date rule")]
    public void DateField_Timestamp_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("dateOfBirth", "2001-04-10T00:00:00Z")),
            validateRequired: false);

        Assert.Equal(
            "Details.dateOfBirth",
            Assert.Single(failures).PropertyName);
        Assert.Contains("YYYY-MM-DD", Assert.Single(failures).ErrorMessage);
    }

    [Fact(DisplayName =
        "A non-date string dateOfBirth fails the date rule")]
    public void DateField_GarbageString_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("dateOfBirth", "not-a-date")),
            validateRequired: false);

        Assert.Equal("Details.dateOfBirth", Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "An out-of-range calendar date fails the date rule")]
    public void DateField_InvalidCalendarDate_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("dateOfBirth", "2024-13-45")),
            validateRequired: false);

        Assert.Equal("Details.dateOfBirth", Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "A numeric dateOfBirth fails the date rule")]
    public void DateField_NumericValue_Fails()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(("passportNumber", "P1"), ("dateOfBirth", 20010410)),
            validateRequired: false);

        Assert.Equal("Details.dateOfBirth", Assert.Single(failures).PropertyName);
    }

    // ------------------------------------------------------------------
    // Number / Boolean / MaxLength (stub schema)
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "A numeric numberField passes and a string numberField fails")]
    public void NumberField_JsonNumberOnly_Passes()
    {
        Assert.Empty(
            AllKindsValidator.Validate(
                DocumentType.Other,
                Obj(("numberField", 42)),
                validateRequired: false));

        var failures = AllKindsValidator.Validate(
            DocumentType.Other,
            Obj(("numberField", "42")),
            validateRequired: false);

        Assert.Equal("Details.numberField", Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "A boolean booleanField passes and a string booleanField fails")]
    public void BooleanField_JsonBooleanOnly_Passes()
    {
        Assert.Empty(
            AllKindsValidator.Validate(
                DocumentType.Other,
                Obj(("booleanField", true)),
                validateRequired: false));

        var failures = AllKindsValidator.Validate(
            DocumentType.Other,
            Obj(("booleanField", "true")),
            validateRequired: false);

        Assert.Equal("Details.booleanField", Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "A numeric value in a Boolean field fails")]
    public void BooleanField_NumericValue_Fails()
    {
        var failures = AllKindsValidator.Validate(
            DocumentType.Other,
            Obj(("booleanField", 1)),
            validateRequired: false);

        Assert.Equal("Details.booleanField", Assert.Single(failures).PropertyName);
    }

    [Fact(DisplayName =
        "MaxLength is enforced on String fields only")]
    public void MaxLength_EnforcedOnStrings()
    {
        Assert.Empty(
            AllKindsValidator.Validate(
                DocumentType.Other,
                Obj(("valueField", "12345")),
                validateRequired: false));

        var failures = AllKindsValidator.Validate(
            DocumentType.Other,
            Obj(("valueField", "123456")),
            validateRequired: false);

        Assert.Equal("Details.valueField", Assert.Single(failures).PropertyName);
        Assert.Contains("5", Assert.Single(failures).ErrorMessage);
    }

    [Fact(DisplayName =
        "Stub schema required field fails only on confirmation")]
    public void RequiredOnly_OnConfirmation()
    {
        var onConfirm = AllKindsValidator.Validate(
            DocumentType.Other,
            Obj(("stringField", "x")),
            validateRequired: true);

        Assert.Equal("Details.requiredField", Assert.Single(onConfirm).PropertyName);

        Assert.Empty(
            AllKindsValidator.Validate(
                DocumentType.Other,
                Obj(("stringField", "x")),
                validateRequired: false));
    }

    // ------------------------------------------------------------------
    // Unknown keys / empty / exemption
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Unknown keys are never rejected and surface no failures")]
    public void UnknownKeys_AreAllowed()
    {
        var failures = Validator.Validate(
            DocumentType.Passport,
            Obj(
                ("passportNumber", "P1"),
                ("vendorSpecific", new { trackingRef = "T1" })),
            validateRequired: true);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "DocumentType.Other is exempt from all schema rules")]
    public void Other_ExemptFromSchemaRules()
    {
        var failures = Validator.Validate(
            DocumentType.Other,
            Obj(("anything", "goes"), ("nested", new { deep = true }), ("n", 5)),
            validateRequired: true);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "An unknown enum value is exempt from all schema rules")]
    public void UnknownEnumValue_ExemptFromSchemaRules()
    {
        var failures = Validator.Validate(
            (DocumentType)12345,
            Obj(("anything", 1)),
            validateRequired: true);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "Empty Details is unconstrained outside confirmation")]
    public void EmptyObject_OutsideConfirmation_Passes()
    {
        Assert.Empty(
            Validator.Validate(
                DocumentType.Passport,
                Obj(),
                validateRequired: false));
    }

    // ------------------------------------------------------------------
    // Input shapes
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "An in-memory IDictionary payload validates identically to JsonElement")]
    public void DictionaryInput_ValidatesLikeJsonElement()
    {
        var dictionary = new Dictionary<string, object?>
        {
            ["passportNumber"] = "P1",
            ["expiryDate"] = "2031-12-31"
        };

        var failures = Validator.Validate(
            DocumentType.Passport,
            dictionary,
            validateRequired: false);

        Assert.Empty(failures);
    }

    [Fact(DisplayName =
        "Null Details is unconstrained outside confirmation")]
    public void NullDetails_OutsideConfirmation_Passes()
    {
        Assert.Empty(
            Validator.Validate(
                DocumentType.Passport,
                null,
                validateRequired: false));
    }

    [Fact(DisplayName =
        "A JSON-null element input is treated as absent Details")]
    public void JsonNullElement_Input_Passes()
    {
        var nullElement = JsonSerializer.SerializeToElement<object?>(null);

        Assert.Empty(
            Validator.Validate(
                DocumentType.Passport,
                nullElement,
                validateRequired: false));
    }
}