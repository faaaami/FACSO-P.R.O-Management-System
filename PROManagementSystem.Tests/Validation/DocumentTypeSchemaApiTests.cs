using System.Net;
using Microsoft.EntityFrameworkCore;
using PROManagementSystem.Application.Common.DocumentSchemas;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.AI.DocumentClassification.RuleBasedSchemas;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Validation;

/// <summary>
/// Task 7B.6.2: read-only document type schema discovery API.
/// Both Admin and Agent expose the shared schema registry through a thin,
/// role-gated GET endpoint. The API must never duplicate schema definitions:
/// responses are compared field-for-field against the single source of truth
/// (<see cref="IDocumentTypeSchemaProvider"/> / <see cref="DocumentTypeSchemaProvider"/>).
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentTypeSchemaApiTests : IntegrationTestBase
{
    private const string AdminSchemaUrl =
        "/api/v1/admin/document-types/{0}/schema";

    private const string AgentSchemaUrl =
        "/api/v1/agent/document-types/{0}/schema";

    private static readonly IDocumentTypeSchemaProvider Registry =
        new DocumentTypeSchemaProvider();

    public DocumentTypeSchemaApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private async Task<HttpResponseMessage> SendAsync(
        string url,
        Guid userId,
        string role,
        string? clientCompanyId = null)
        => await ScopeClient.SendAsync(
            ScopeGet(url, userId, role, clientCompanyId));

    private static async Task<SchemaEnvelope?> ReadSchemaAsync(
        HttpResponseMessage response)
        => await ReadJsonAsync<SchemaEnvelope>(response);

    private void AssertSchemaMatchesRegistry(
        DocumentType documentType,
        SchemaEnvelope? envelope)
    {
        Assert.NotNull(envelope);
        Assert.True(envelope!.Success);
        Assert.NotNull(envelope.Data);

        var expected = Registry.Get(documentType);
        var actual = envelope.Data!.Fields;

        // Field order must be preserved (registry order is the display order).
        Assert.Equal(expected.Count, actual.Count);

        for (var i = 0; i < expected.Count; i++)
        {
            var definition = expected[i];
            var dto = actual[i];

            Assert.Equal(definition.Key, dto.Key);
            Assert.Equal(definition.Label, dto.Label);
            Assert.Equal(definition.ValueKind, dto.ValueKind);
            Assert.Equal(definition.Required, dto.Required);
            Assert.Equal(definition.MaxLength, dto.MaxLength);
        }
    }

    // ------------------------------------------------------------------
    // Admin API
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Admin: Passport schema returns 200 and matches the registry")]
    public async Task Admin_PassportSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.Passport,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: Visa schema returns 200 and matches the registry")]
    public async Task Admin_VisaSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Visa)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.Visa,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: LaborCard schema returns 200 and matches the registry")]
    public async Task Admin_LaborCardSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.LaborCard)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.LaborCard,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: EmiratesId schema returns 200 and matches the registry")]
    public async Task Admin_EmiratesIdSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.EmiratesId)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.EmiratesId,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: TradeLicense schema returns 200 and matches the registry")]
    public async Task Admin_TradeLicenseSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.TradeLicense)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.TradeLicense,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: EstablishmentCard schema returns 200 and matches the registry")]
    public async Task Admin_EstablishmentCardSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.EstablishmentCard)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.EstablishmentCard,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Admin: Other schema returns 200 with an empty fields list")]
    public async Task Admin_OtherSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Other)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadSchemaAsync(response);

        Assert.NotNull(envelope?.Data);
        Assert.NotNull(envelope!.Data!.Fields);
        Assert.Empty(envelope.Data.Fields);
    }

    // ------------------------------------------------------------------
    // Agent API
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Agent: Passport schema returns 200 and matches the registry")]
    public async Task Agent_PassportSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.Passport,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Agent: Visa schema returns 200 and matches the registry")]
    public async Task Agent_VisaSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Visa)),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await EnsureOk(response, HttpStatusCode.OK);

        AssertSchemaMatchesRegistry(
            DocumentType.Visa,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Agent: Other schema returns 200 with an empty fields list")]
    public async Task Agent_OtherSchema_Returns200()
    {
        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Other)),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await EnsureOk(response, HttpStatusCode.OK);

        var envelope = await ReadSchemaAsync(response);

        Assert.NotNull(envelope?.Data);
        Assert.NotNull(envelope!.Data!.Fields);
        Assert.Empty(envelope.Data.Fields);
    }

    // ------------------------------------------------------------------
    // Contract tests: API exactly mirrors the registry
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Contract: Passport response fields match the registry exactly")]
    public async Task Contract_PassportFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.Passport,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: Visa response fields match the registry exactly")]
    public async Task Contract_VisaFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Visa)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.Visa,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: LaborCard response fields match the registry exactly")]
    public async Task Contract_LaborCardFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.LaborCard)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.LaborCard,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: EmiratesId response fields match the registry exactly")]
    public async Task Contract_EmiratesIdFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.EmiratesId)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.EmiratesId,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: TradeLicense response fields match the registry exactly")]
    public async Task Contract_TradeLicenseFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.TradeLicense)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.TradeLicense,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: EstablishmentCard response fields match the registry exactly")]
    public async Task Contract_EstablishmentCardFieldsMatchRegistry()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.EstablishmentCard)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        AssertSchemaMatchesRegistry(
            DocumentType.EstablishmentCard,
            await ReadSchemaAsync(response));
    }

    [Fact(DisplayName =
        "Contract: Other schema has no fields (matches registry)")]
    public async Task Contract_OtherHasEmptyFields()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Other)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        var envelope = await ReadSchemaAsync(response);

        Assert.NotNull(envelope?.Data);
        Assert.Empty(envelope!.Data!.Fields);
        Assert.Empty(Registry.Get(DocumentType.Other));
    }

    // ------------------------------------------------------------------
    // Field property preservation
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Properties: Required flags match the registry for every type")]
    public async Task Properties_RequiredFlagsArePreserved()
    {
        foreach (var documentType in SupportedDocumentTypes())
        {
            var envelope = await ReadSchemaAsync(
                await SendAsync(
                    string.Format(
                        AdminSchemaUrl,
                        documentType.ToString()),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            var expected = Registry.Get(documentType);
            Assert.NotNull(envelope?.Data);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    expected[i].Required,
                    envelope!.Data!.Fields[i].Required);
            }
        }
    }

    [Fact(DisplayName =
        "Properties: MaxLength values match the registry for every type")]
    public async Task Properties_MaxLengthsArePreserved()
    {
        foreach (var documentType in SupportedDocumentTypes())
        {
            var envelope = await ReadSchemaAsync(
                await SendAsync(
                    string.Format(
                        AdminSchemaUrl,
                        documentType.ToString()),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            var expected = Registry.Get(documentType);
            Assert.NotNull(envelope?.Data);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    expected[i].MaxLength,
                    envelope!.Data!.Fields[i].MaxLength);
            }
        }
    }

    [Fact(DisplayName =
        "Properties: ValueKinds match the registry for every type")]
    public async Task Properties_ValueKindsArePreserved()
    {
        foreach (var documentType in SupportedDocumentTypes())
        {
            var envelope = await ReadSchemaAsync(
                await SendAsync(
                    string.Format(
                        AdminSchemaUrl,
                        documentType.ToString()),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            var expected = Registry.Get(documentType);
            Assert.NotNull(envelope?.Data);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    expected[i].ValueKind,
                    envelope!.Data!.Fields[i].ValueKind);
            }
        }
    }

    [Fact(DisplayName =
        "Properties: Labels match the registry for every type")]
    public async Task Properties_LabelsArePreserved()
    {
        foreach (var documentType in SupportedDocumentTypes())
        {
            var envelope = await ReadSchemaAsync(
                await SendAsync(
                    string.Format(
                        AdminSchemaUrl,
                        documentType.ToString()),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            var expected = Registry.Get(documentType);
            Assert.NotNull(envelope?.Data);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    expected[i].Label,
                    envelope!.Data!.Fields[i].Label);
            }
        }
    }

    [Fact(DisplayName =
        "Properties: field order is the registry order (not alphabetical)")]
    public async Task Properties_FieldOrderIsPreserved()
    {
        foreach (var documentType in SupportedDocumentTypes())
        {
            var envelope = await ReadSchemaAsync(
                await SendAsync(
                    string.Format(
                        AdminSchemaUrl,
                        documentType.ToString()),
                    TestDataSeeder.Ids.Admin,
                    "Admin"));

            var expected = Registry.Get(documentType);
            Assert.NotNull(envelope?.Data);

            for (var i = 0; i < expected.Count; i++)
            {
                Assert.Equal(
                    expected[i].Key,
                    envelope!.Data!.Fields[i].Key);
            }

            // The keys must NOT be alphabetically sorted unless the registry
            // itself happens to be alphabetical.
            var keys = envelope.Data!.Fields
                .Select(f => f.Key)
                .ToList();
            var sorted = keys.OrderBy(x => x).ToList();
            Assert.Equal(keys.Count, sorted.Count);
        }
    }

    // ------------------------------------------------------------------
    // Security
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Anonymous cannot access the Admin schema endpoint (401)")]
    public async Task Anonymous_CannotAccessAdminSchema()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)));

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Agent role cannot access the Admin schema endpoint (403)")]
    public async Task Agent_CannotAccessAdminSchema()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Anonymous cannot access the Agent schema endpoint (401)")]
    public async Task Anonymous_CannotAccessAgentSchema()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            string.Format(AgentSchemaUrl, nameof(DocumentType.Passport)));

        var response = await ScopeClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact(DisplayName =
        "Admin role cannot access the Agent schema endpoint (403)")]
    public async Task Admin_CannotAccessAgentSchema()
    {
        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Client role cannot access the Admin schema endpoint (403)")]
    public async Task Client_CannotAccessAdminSchema()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName =
        "Client role cannot access the Agent schema endpoint (403)")]
    public async Task Client_CannotAccessAgentSchema()
    {
        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.ClientUserA,
            "Client",
            TestDataSeeder.Ids.CompanyA.ToString());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------------------------------
    // Invalid input
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Invalid document type returns the standard 400 error envelope")]
    public async Task InvalidDocumentType_ReturnsStandardError()
    {
        var response = await SendAsync(
            string.Format(AdminSchemaUrl, "NotARealType"),
            TestDataSeeder.Ids.Admin,
            "Admin");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var envelope =
            await ReadJsonAsync<ErrorEnvelope>(response);

        Assert.NotNull(envelope);
        Assert.False(envelope!.Success);
        Assert.NotNull(envelope.Error);
        Assert.Equal("BAD_REQUEST", envelope.Error.Code);
    }

    // ------------------------------------------------------------------
    // Read-only
    // ------------------------------------------------------------------

    [Fact(DisplayName =
        "Schema endpoint does not modify the database")]
    public async Task ReadOnly_DoesNotModifyDatabase()
    {
        var before = await CountsAsync();

        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.Passport)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var after = await CountsAsync();

        Assert.Equal(before, after);
    }

    [Fact(DisplayName =
        "Schema endpoint does not modify Documents")]
    public async Task ReadOnly_DoesNotModifyDocuments()
    {
        var before = 0;
        await WithScopeDbContextAsync(db =>
        {
            before = db.Documents.Count();
        });

        var response = await SendAsync(
            string.Format(AgentSchemaUrl, nameof(DocumentType.Visa)),
            TestDataSeeder.Ids.AgentA,
            "Agent",
            TestDataSeeder.Ids.CompanyA.ToString());

        await EnsureOk(response, HttpStatusCode.OK);

        var after = 0;
        await WithScopeDbContextAsync(db =>
        {
            after = db.Documents.Count();
        });

        Assert.Equal(before, after);
    }

    [Fact(DisplayName =
        "Schema endpoint does not modify DocumentExtractions")]
    public async Task ReadOnly_DoesNotModifyExtractions()
    {
        var before = 0;
        await WithScopeDbContextAsync(db =>
        {
            before = db.DocumentExtractions.Count();
        });

        var response = await SendAsync(
            string.Format(AdminSchemaUrl, nameof(DocumentType.TradeLicense)),
            TestDataSeeder.Ids.Admin,
            "Admin");

        await EnsureOk(response, HttpStatusCode.OK);

        var after = 0;
        await WithScopeDbContextAsync(db =>
        {
            after = db.DocumentExtractions.Count();
        });

        Assert.Equal(before, after);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static IEnumerable<DocumentType> SupportedDocumentTypes()
    {
        yield return DocumentType.Passport;
        yield return DocumentType.Visa;
        yield return DocumentType.LaborCard;
        yield return DocumentType.EmiratesId;
        yield return DocumentType.TradeLicense;
        yield return DocumentType.EstablishmentCard;
        yield return DocumentType.Other;
    }

    private async Task<(int Documents, int Extractions, int AuditLogs)>
        CountsAsync()
    {
        var counts = (0, 0, 0);

        await WithScopeDbContextAsync(db =>
        {
            counts = (
                db.Documents.Count(),
                db.DocumentExtractions.Count(),
                db.AuditLogs.Count());
        });

        return counts;
    }

    // ------------------------------------------------------------------
    // Deserialization envelopes
    // ------------------------------------------------------------------

    private sealed class SchemaEnvelope
    {
        public bool Success { get; set; }

        public SchemaPayload? Data { get; set; }
    }

    private sealed class SchemaPayload
    {
        public DocumentType DocumentType { get; set; }

        public List<FieldPayload> Fields { get; set; } = new();
    }

    private sealed class FieldPayload
    {
        public string Key { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public DocumentFieldValueKind ValueKind { get; set; }

        public bool Required { get; set; }

        public int? MaxLength { get; set; }
    }

    private sealed class ErrorEnvelope
    {
        public bool Success { get; set; }

        public ErrorPayload? Error { get; set; }
    }

    private sealed class ErrorPayload
    {
        public string Code { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}