using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="DocumentTypeSchemaProvider"/> (Task 7B.1). These
/// verify the registry is deterministic metadata: the expected schemas exist,
/// keys are stable camelCase contracts that never repeat within a schema,
/// ValueKind/MaxLength metadata is well-formed, required flags are
/// conservative and deterministic, and callers can never mutate the global
/// registry. They do not test validation, persistence, or AI behavior — none
/// of which exist in this layer by design.
/// </summary>
public class DocumentTypeSchemaProviderTests
{
    private static readonly IDocumentTypeSchemaProvider Provider =
        new DocumentTypeSchemaProvider();

    [Fact(DisplayName =
        "Passport has an expected schema with the representative fields")]
    public void Passport_Schema_HasRepresentativeFields()
    {
        var schema = Provider.Get(DocumentType.Passport);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "passportNumber");
        Assert.Contains(schema, f => f.Key == "nationality");
        Assert.Contains(schema, f => f.Key == "dateOfBirth");
        Assert.Contains(schema, f => f.Key == "expiryDate");
        Assert.Contains(schema, f => f.Key == "issuingAuthority");
    }

    [Fact(DisplayName =
        "Visa has an expected schema")]
    public void Visa_Schema_ExistsAndHasKeys()
    {
        var schema = Provider.Get(DocumentType.Visa);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "visaNumber");
        Assert.Contains(schema, f => f.Key == "visaType");
        Assert.Contains(schema, f => f.Key == "entryPermitNumber");
        Assert.Contains(schema, f => f.Key == "sponsorName");
    }

    [Fact(DisplayName =
        "LaborCard has an expected schema")]
    public void LaborCard_Schema_ExistsAndHasKeys()
    {
        var schema = Provider.Get(DocumentType.LaborCard);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "laborCardNumber");
        Assert.Contains(schema, f => f.Key == "employeeNumber");
        Assert.Contains(schema, f => f.Key == "occupation");
        Assert.Contains(schema, f => f.Key == "employerName");
    }

    [Fact(DisplayName =
        "EmiratesId has an expected schema")]
    public void EmiratesId_Schema_ExistsAndHasKeys()
    {
        var schema = Provider.Get(DocumentType.EmiratesId);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "idNumber");
        Assert.Contains(schema, f => f.Key == "cardNumber");
        Assert.Contains(schema, f => f.Key == "nationality");
    }

    [Fact(DisplayName =
        "TradeLicense has an expected schema with the representative fields")]
    public void TradeLicense_Schema_HasRepresentativeFields()
    {
        var schema = Provider.Get(DocumentType.TradeLicense);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "licenseNumber");
        Assert.Contains(schema, f => f.Key == "legalName");
        Assert.Contains(schema, f => f.Key == "tradeName");
        Assert.Contains(schema, f => f.Key == "expiryDate");
        Assert.Contains(schema, f => f.Key == "issuingAuthority");
    }

    [Fact(DisplayName =
        "EstablishmentCard has an expected schema with the representative fields")]
    public void EstablishmentCard_Schema_HasRepresentativeFields()
    {
        var schema = Provider.Get(DocumentType.EstablishmentCard);

        Assert.NotEmpty(schema);
        Assert.Contains(schema, f => f.Key == "establishmentNumber");
        Assert.Contains(schema, f => f.Key == "companyName");
        Assert.Contains(schema, f => f.Key == "economicDepartment");
    }

    [Fact(DisplayName =
        "Other resolves to an empty schema")]
    public void Other_ReturnsEmptySchema()
    {
        Assert.Empty(Provider.Get(DocumentType.Other));
    }

    [Fact(DisplayName =
        "Every schema has unique keys")]
    public void EverySchema_HasUniqueKeys()
    {
        foreach (var field in FieldsOfEveryNonEmptySchema())
        {
            var keys = Provider.Get(field.DocumentType)
                .Select(f => f.Key)
                .ToList();

            Assert.Equal(
                keys.Count,
                keys.Distinct().Count());
        }
    }

    [Fact(DisplayName =
        "Every key follows the stable camelCase naming convention")]
    public void EveryKey_IsCamelCase()
    {
        var camelCase = new System.Text.RegularExpressions.Regex(
            "^[a-z][a-zA-Z0-9]*$");

        foreach (var field in FieldsOfEveryNonEmptySchema())
        {
            foreach (var definition in Provider.Get(field.DocumentType))
            {
                Assert.Matches(
                    camelCase,
                    definition.Key);
            }
        }
    }

    [Fact(DisplayName =
        "Every field exposes a valid DocumentFieldValueKind")]
    public void EveryField_HasValidValueKind()
    {
        var knownKinds = Enum.GetValues<DocumentFieldValueKind>();

        foreach (var field in FieldsOfEveryNonEmptySchema())
        {
            foreach (var definition in Provider.Get(field.DocumentType))
            {
                Assert.Contains(definition.ValueKind, knownKinds);
            }
        }
    }

    [Fact(DisplayName =
        "Only each type's primary identifier is Required")]
    public void RequiredFlags_AreDeterministicAndConservative()
    {
        var requiredKeys = new Dictionary<DocumentType, string>
        {
            [DocumentType.Passport] = "passportNumber",
            [DocumentType.Visa] = "visaNumber",
            [DocumentType.LaborCard] = "laborCardNumber",
            [DocumentType.EmiratesId] = "idNumber",
            [DocumentType.TradeLicense] = "licenseNumber",
            [DocumentType.EstablishmentCard] = "establishmentNumber",
        };

        foreach (var (documentType, requiredKey) in requiredKeys)
        {
            var schema = Provider.Get(documentType);

            var required =
                schema.Where(f => f.Required).ToList();

            Assert.Single(required);
            Assert.Equal(requiredKey, required[0].Key);
        }

        Assert.DoesNotContain(
            Provider.Get(DocumentType.Other),
            f => f.Required);
    }

    [Fact(DisplayName =
        "MaxLength is null for any non-String field")]
    public void MaxLength_AbsentForNonStringFields()
    {
        foreach (var field in FieldsOfEveryNonEmptySchema())
        {
            foreach (var definition in Provider.Get(field.DocumentType))
            {
                if (definition.ValueKind != DocumentFieldValueKind.String)
                {
                    Assert.Null(definition.MaxLength);
                }
            }
        }
    }

    [Fact(DisplayName =
        "Every defined DocumentType returns a non-null schema")]
    public void Provider_NeverReturnsNull()
    {
        foreach (var documentType in Enum.GetValues<DocumentType>())
        {
            Assert.NotNull(Provider.Get(documentType));
        }
    }

    [Fact(DisplayName =
        "An unknown enum value safely resolves to an empty schema")]
    public void UnknownEnumValue_ReturnsEmptySchema()
    {
        var unknown = (DocumentType)12345;

        Assert.Empty(Provider.Get(unknown));
    }

    [Fact(DisplayName =
        "Returned schemas are read-only and cannot mutate the registry")]
    public void Schemas_AreReadOnlyAndShareStaticInstances()
    {
        foreach (var field in FieldsOfEveryNonEmptySchema())
        {
            var schema = Provider.Get(field.DocumentType);

            // Arrays back the returned IReadOnlyList; ICollection<T>.IsReadOnly
            // confirms the runtime collection cannot be structurally modified.
            var collection =
                Assert.IsAssignableFrom<ICollection<DocumentFieldDefinition>>(
                    schema);

            Assert.True(collection.IsReadOnly);

            // The same static instance is returned on every call — callers can
            // never obtain a mutable per-call copy of the global registry.
            Assert.Same(schema, Provider.Get(field.DocumentType));
        }
    }

    private static IEnumerable<(DocumentType DocumentType, string Key)>
        FieldsOfEveryNonEmptySchema()
    {
        return new (DocumentType, string)[]
        {
            (DocumentType.Passport, "passportNumber"),
            (DocumentType.Visa, "visaNumber"),
            (DocumentType.LaborCard, "laborCardNumber"),
            (DocumentType.EmiratesId, "idNumber"),
            (DocumentType.TradeLicense, "licenseNumber"),
            (DocumentType.EstablishmentCard, "establishmentNumber"),
        };
    }
}