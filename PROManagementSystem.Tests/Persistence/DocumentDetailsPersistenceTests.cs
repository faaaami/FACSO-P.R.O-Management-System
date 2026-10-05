using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Infrastructure.Persistence;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Persistence;

/// <summary>
/// Task 7B.2: persistence tests for <see cref="Document.Details"/>, the
/// authoritative human-confirmed type-specific details stored as a jsonb
/// document. The CLR representation is a JSON string produced with
/// System.Text.Json (Npgsql's documented string-jsonb mapping — generic
/// dictionaries require an out-of-scope EnableDynamicJson opt-in). "No
/// details" is always the empty object "{}", never null.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class DocumentDetailsPersistenceTests : IntegrationTestBase
{
    public DocumentDetailsPersistenceTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName =
        "Document.Details maps to a non-null jsonb column with an empty-object default")]
    public async Task Details_MapsToJsonb_WithConstantEmptyDefault()
    {
        await using var db = await CreateScopeDbContextAsync();

        var mappedType = db.Model
            .FindEntityType(typeof(Document))!
            .FindProperty(nameof(Document.Details))!
            .GetColumnType();

        Assert.Equal("jsonb", mappedType);

        await using (var connection = new NpgsqlConnection(
            Fixture.Container.GetConnectionString()))
        {
            await connection.OpenAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT
                    data_type,
                    is_nullable,
                    column_default
                FROM information_schema.columns
                WHERE table_name = 'documents'
                  AND column_name = 'details';
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal("jsonb", reader.GetString(0));
            Assert.Equal("NO", reader.GetString(1));
            Assert.Equal("'{}'::jsonb", reader.GetString(2));
        }
    }

    [Fact(DisplayName =
        "A new Document persists type-specific details with every JSON value kind")]
    public async Task NewDocument_PersistsDetails_AllValueKinds()
    {
        var detailsJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                // String
                ["passportNumber"] = "P90321",
                ["nationality"] = "Indian",
                // Date as an ISO-8601 date-only string (schema ValueKind.Date)
                ["dateOfBirth"] = "2001-04-10",
                ["placeOfBirth"] = "Mumbai",
                ["sex"] = "M",
                // Date
                ["issueDate"] = "2024-01-15",
                ["expiryDate"] = "2034-01-14",
                ["issuingAuthority"] = "ICP DUBAI",
                // Number
                ["sample"] = null
            });

        var documentId = Guid.NewGuid();

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                ClientEntityId = null,
                Type = DocumentType.Passport,
                DocumentNumber = "P-7B2-0001",
                IssueDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                ExpiryDate = new DateTime(2034, 1, 14, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                Details = detailsJson,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.Documents
                .AsNoTracking()
                .SingleAsync(x => x.Id == documentId);

            AssertJsonEqual(detailsJson, stored.Details);
        }
    }

    [Fact(DisplayName =
        "A new Document with the default Details persists the empty object")]
    public async Task NewDocument_DefaultDetails_PersistsEmptyObject()
    {
        var documentId = Guid.NewGuid();

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.Visa,
                DocumentNumber = "V-7B2-0002",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.Documents
                .AsNoTracking()
                .SingleAsync(x => x.Id == documentId);

            Assert.NotNull(stored.Details);
            Assert.Equal("{}", stored.Details);
        }
    }

    [Fact(DisplayName =
        "Legacy documents without the column read back the empty object")]
    public async Task LegacyRow_WithoutDetailsColumn_ReadsAsEmptyObject()
    {
        var documentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var db = await CreateScopeDbContextAsync())
        {
            // A row written exactly like the pre-7B.2 schema: no details column.
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO documents
                    (id, employee_id, type, document_number, is_active,
                     created_at, updated_at, is_deleted)
                VALUES
                    ({0}, {1}, 'Passport', 'LEGACY-RAW-0001', true, {2}, {2}, false);
                """,
                documentId,
                TestDataSeeder.Ids.EmployeeA,
                now);
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.Documents
                .AsNoTracking()
                .SingleAsync(x => x.Id == documentId);

            Assert.NotNull(stored.Details);
            Assert.Equal("{}", stored.Details);
        }
    }

    [Fact(DisplayName =
        "Replacing the Details string persists (safe write pattern)")]
    public async Task Replacement_DetailsJson_Persists()
    {
        var documentId = Guid.NewGuid();

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.EmiratesId,
                DocumentNumber = "E-7B2-0003",
                Details = "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        var updatedJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["idNumber"] = "784-2026-0000000-1",
                ["nationality"] = "Indian",
                ["expiryDate"] = "2026-09-30"
            });

        await using (var db = await CreateScopeDbContextAsync())
        {
            var tracked = await db.Documents
                .SingleAsync(x => x.Id == documentId);

            // Writers must replace the whole value (strings mutate only via
            // assignment; in-place JSON edits are handled at the app layer).
            tracked.Details = updatedJson;
            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.Documents
                .AsNoTracking()
                .SingleAsync(x => x.Id == documentId);

            AssertJsonEqual(updatedJson, stored.Details);
        }
    }

    [Fact(DisplayName =
        "Details do not affect the hot document query predicates")]
    public async Task Details_DoNotAffectExistingQueries()
    {
        var documentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var detailsJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["tradeName"] = "Test Trading LLC"
            });

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.Documents.Add(new Document
            {
                Id = documentId,
                EmployeeId = TestDataSeeder.Ids.EmployeeA,
                Type = DocumentType.TradeLicense,
                DocumentNumber = "TL-7B2-0004",
                ExpiryDate = now.AddDays(10),
                IsActive = true,
                Details = detailsJson,
                CreatedAt = now,
                UpdatedAt = now,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            // Mirrors the hot predicates: owner, type, expiry window, state.
            var byOwnerAndType = await db.Documents
                .AsNoTracking()
                .AnyAsync(x => x.EmployeeId == TestDataSeeder.Ids.EmployeeA
                    && x.Type == DocumentType.TradeLicense);

            var inExpiryWindow = await db.Documents
                .AsNoTracking()
                .AnyAsync(x => x.Id == documentId
                    && x.ExpiryDate > now
                    && x.ExpiryDate < now.AddDays(30));

            var activeAndNotDeleted = await db.Documents
                .AsNoTracking()
                .AnyAsync(x => x.Id == documentId
                    && x.IsActive
                    && !x.IsDeleted);

            Assert.True(byOwnerAndType);
            Assert.True(inExpiryWindow);
            Assert.True(activeAndNotDeleted);
        }
    }

    [Fact(DisplayName =
        "No OCR text is ever persisted on Document or its table")]
    public async Task NoOcrText_IsPersisted()
    {
        var documentProperties = typeof(Document)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(documentProperties, n =>
            n.Contains("Ocr", StringComparison.OrdinalIgnoreCase)
            || n.Contains("RawText", StringComparison.OrdinalIgnoreCase));

        await using var connection = new NpgsqlConnection(
            Fixture.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'documents';
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        // The table exposes no OCR/raw-text column; details jsonb is the only
        // place type-specific text can land, and it is written explicitly.
        Assert.DoesNotContain(names, n =>
            n.Contains("ocr", StringComparison.OrdinalIgnoreCase)
            || n.Contains("raw_text", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("details", names);
    }

    private static void AssertJsonEqual(
        string expected,
        string actual)
    {
        Assert.NotNull(actual);

        using var expectedDoc = JsonDocument.Parse(expected);
        using var actualDoc = JsonDocument.Parse(actual);

        Assert.True(
            JsonElement.DeepEquals(
                expectedDoc.RootElement,
                actualDoc.RootElement),
            $"JSON mismatch.{Environment.NewLine}Expected: {expected}{Environment.NewLine}Actual:   {actual}");
    }
}