using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Domain.Enums;

namespace PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;

/// <summary>
/// Deterministic schema registry (Task 7B.1) for type-specific document
/// fields. Every supported <see cref="DocumentType"/> carries a fixed,
/// conservative read-only list of <see cref="DocumentFieldDefinition"/>; the
/// list for <see cref="DocumentType.Other"/> — and for any unhandled enum
/// value — is intentionally empty.
///
/// Field keys are stable camelCase contracts and never repeat within a
/// schema. Identical concepts (dateOfBirth, issueDate, expiryDate,
/// nationality) reuse the same key across types. Required is reserved for
/// each type's primary identifier; everything else is optional metadata for
/// future validation, review, and AI extraction.
///
/// This layer is metadata only: it does not validate values, does not touch
/// persistence, and has no dependency on EF Core, Npgsql, or OCR.
/// </summary>
public sealed class DocumentTypeSchemaProvider
    : IDocumentTypeSchemaProvider
{
    private static readonly IReadOnlyList<DocumentFieldDefinition> Empty =
        Array.Empty<DocumentFieldDefinition>();

    private static readonly IReadOnlyDictionary<
        DocumentType,
        IReadOnlyList<DocumentFieldDefinition>> Schemas =
        new Dictionary<DocumentType, IReadOnlyList<DocumentFieldDefinition>>
        {
            [DocumentType.Passport] = new[]
            {
                new DocumentFieldDefinition(
                    "passportNumber",
                    "Passport Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "nationality",
                    "Nationality",
                    DocumentFieldValueKind.String,
                    MaxLength: 100),

                new DocumentFieldDefinition(
                    "dateOfBirth",
                    "Date of Birth",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "placeOfBirth",
                    "Place of Birth",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),

                new DocumentFieldDefinition(
                    "sex",
                    "Sex",
                    DocumentFieldValueKind.String,
                    MaxLength: 20),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "issuingAuthority",
                    "Issuing Authority",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),
            },

            [DocumentType.Visa] = new[]
            {
                new DocumentFieldDefinition(
                    "visaNumber",
                    "Visa Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "visaType",
                    "Visa Type",
                    DocumentFieldValueKind.String,
                    MaxLength: 100),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "entryPermitNumber",
                    "Entry Permit Number",
                    DocumentFieldValueKind.String,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "uidNumber",
                    "UID Number",
                    DocumentFieldValueKind.String,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "sponsorName",
                    "Sponsor Name",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),
            },

            [DocumentType.EmiratesId] = new[]
            {
                new DocumentFieldDefinition(
                    "idNumber",
                    "ID Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "cardNumber",
                    "Card Number",
                    DocumentFieldValueKind.String,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "nationality",
                    "Nationality",
                    DocumentFieldValueKind.String,
                    MaxLength: 100),

                new DocumentFieldDefinition(
                    "dateOfBirth",
                    "Date of Birth",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),
            },

            [DocumentType.LaborCard] = new[]
            {
                new DocumentFieldDefinition(
                    "laborCardNumber",
                    "Labor Card Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "employeeNumber",
                    "Employee Number",
                    DocumentFieldValueKind.String,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "occupation",
                    "Occupation",
                    DocumentFieldValueKind.String,
                    MaxLength: 100),

                new DocumentFieldDefinition(
                    "employerName",
                    "Employer Name",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),
            },

            [DocumentType.TradeLicense] = new[]
            {
                new DocumentFieldDefinition(
                    "licenseNumber",
                    "License Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "legalName",
                    "Legal Name",
                    DocumentFieldValueKind.String,
                    MaxLength: 300),

                new DocumentFieldDefinition(
                    "tradeName",
                    "Trade Name",
                    DocumentFieldValueKind.String,
                    MaxLength: 300),

                new DocumentFieldDefinition(
                    "issuingAuthority",
                    "Issuing Authority",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),

                new DocumentFieldDefinition(
                    "licenseType",
                    "License Type",
                    DocumentFieldValueKind.String,
                    MaxLength: 100),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),
            },

            [DocumentType.EstablishmentCard] = new[]
            {
                new DocumentFieldDefinition(
                    "establishmentNumber",
                    "Establishment Number",
                    DocumentFieldValueKind.String,
                    Required: true,
                    MaxLength: 50),

                new DocumentFieldDefinition(
                    "companyName",
                    "Company Name",
                    DocumentFieldValueKind.String,
                    MaxLength: 300),

                new DocumentFieldDefinition(
                    "economicDepartment",
                    "Economic Department",
                    DocumentFieldValueKind.String,
                    MaxLength: 200),

                new DocumentFieldDefinition(
                    "issueDate",
                    "Issue Date",
                    DocumentFieldValueKind.Date),

                new DocumentFieldDefinition(
                    "expiryDate",
                    "Expiry Date",
                    DocumentFieldValueKind.Date),
            },
        };

    public IReadOnlyList<DocumentFieldDefinition> Get(
        DocumentType documentType)
    {
        return Schemas.TryGetValue(
            documentType,
            out var schema)
            ? schema
            : Empty;
    }
}