using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PROManagementSystem.Domain.Entities;
using PROManagementSystem.Domain.Enums;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Persistence;

/// <summary>
/// Task 7B.2: persistence tests for
/// <see cref="DocumentExtraction.SuggestedDetails"/>, the non-authoritative
/// extraction/AI suggestions stored as a jsonb document. The CLR
/// representation is a JSON string produced with System.Text.Json. "No
/// suggestions" is always the empty object "{}", never null.
/// </summary>
[Collection(DatabaseCollection.Name)]
public class SuggestedDetailsPersistenceTests : IntegrationTestBase
{
    public SuggestedDetailsPersistenceTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    [Fact(DisplayName =
        "DocumentExtraction.SuggestedDetails maps to a non-null jsonb column with an empty-object default")]
    public async Task SuggestedDetails_MapsToJsonb_WithConstantEmptyDefault()
    {
        await using var db = await CreateScopeDbContextAsync();

        var mappedType = db.Model
            .FindEntityType(typeof(DocumentExtraction))!
            .FindProperty(nameof(DocumentExtraction.SuggestedDetails))!
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
                WHERE table_name = 'document_extractions'
                  AND column_name = 'suggested_details';
                """;
            await using var reader = await command.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());

            Assert.Equal("jsonb", reader.GetString(0));
            Assert.Equal("NO", reader.GetString(1));
            Assert.Equal("'{}'::jsonb", reader.GetString(2));
        }
    }

    [Fact(DisplayName =
        "A new extraction persists SuggestedDetails with every JSON value kind")]
    public async Task NewExtraction_PersistsSuggestedDetails_AllValueKinds()
    {
        var suggestedJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                // String
                ["passportNumber"] = "P90321",
                ["nationality"] = "Indian",
                // Date as ISO-8601 date-only strings
                ["dateOfBirth"] = "2001-04-10",
                ["issueDate"] = "2024-01-15",
                ["expiryDate"] = "2034-01-14",
                // Boolean
                ["isPlaceOfBirthAvailable"] = false,
                // Nested object
                ["issuingAuthority"] = new Dictionary<string, object?>
                {
                    ["name"] = "ICP Dubai",
                    ["emirate"] = "Dubai"
                },
                // Unknown/extraneous key tolerated and preserved
                ["extraneousKey"] = "keep-me",
                // JSON null coalesced to a null value by STJ
                ["sample"] = null
            });

        var extractionId = Guid.NewGuid();

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = extractionId,
                FileName = "passport.pdf",
                FileUrl = null,
                ContentType = "application/pdf",
                FileSize = 2048,
                SuggestedType = DocumentType.Passport,
                SuggestedDocumentNumber = "P90321",
                SuggestedIssueDate = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                SuggestedExpiryDate = new DateTime(2034, 1, 14, 0, 0, 0, DateTimeKind.Utc),
                SuggestedOwnerType = "Employee",
                SuggestedDetails = suggestedJson,
                Status = "Pending",
                ExpiresAt = DateTime.UtcNow.AddHours(1),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.DocumentExtractions
                .AsNoTracking()
                .SingleAsync(x => x.Id == extractionId);

            AssertJsonEqual(suggestedJson, stored.SuggestedDetails);

            // The lifecycle fields survive untouched alongside the JSON.
            Assert.Equal(DocumentType.Passport, stored.SuggestedType);
            Assert.Equal("Pending", stored.Status);
            Assert.Equal("Employee", stored.SuggestedOwnerType);
        }
    }

    [Fact(DisplayName =
        "A new extraction with the default SuggestedDetails persists the empty object")]
    public async Task NewExtraction_DefaultSuggestedDetails_PersistsEmptyObject()
    {
        var extractionId = Guid.NewGuid();

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.DocumentExtractions.Add(new DocumentExtraction
            {
                Id = extractionId,
                FileName = "visa.pdf",
                ContentType = "application/pdf",
                FileSize = 1024,
                Status = "Pending",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.DocumentExtractions
                .AsNoTracking()
                .SingleAsync(x => x.Id == extractionId);

            Assert.NotNull(stored.SuggestedDetails);
            Assert.Equal("{}", stored.SuggestedDetails);
        }
    }

    [Fact(DisplayName =
        "Legacy extractions without the column read back the empty object")]
    public async Task LegacyRow_WithoutSuggestedDetails_ReadsAsEmptyObject()
    {
        var extractionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        await using (var db = await CreateScopeDbContextAsync())
        {
            await db.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO document_extractions
                    ("Id", "FileName", "ContentType", "FileSize",
                     "Status", "CreatedAt", "UpdatedAt",
                     "IsDeleted")
                VALUES
                    ({0}, 'legacy-scan.pdf', 'application/pdf', 512,
                     'Pending', {1}, {1}, false);
                """,
                extractionId,
                now);
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            var stored = await db.DocumentExtractions
                .AsNoTracking()
                .SingleAsync(x => x.Id == extractionId);

            Assert.NotNull(stored.SuggestedDetails);
            Assert.Equal("{}", stored.SuggestedDetails);
        }
    }

    [Fact(DisplayName =
        "SuggestedDetails does not affect extraction lifecycle queries")]
    public async Task SuggestedDetails_DoesNotAffectExtractionLifecycle()
    {
        var now = DateTime.UtcNow;
        var pendingId = Guid.NewGuid();
        var expiredId = Guid.NewGuid();
        var suggestedJson = JsonSerializer.Serialize(
            new Dictionary<string, object?>
            {
                ["passportNumber"] = "P90321"
            });

        await using (var db = await CreateScopeDbContextAsync())
        {
            db.DocumentExtractions.AddRange(
                new DocumentExtraction
                {
                    Id = pendingId,
                    FileName = "pending.pdf",
                    ContentType = "application/pdf",
                    FileSize = 128,
                    SuggestedDetails = suggestedJson,
                    Status = "Pending",
                    ExpiresAt = now.AddHours(1),
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false
                },
                new DocumentExtraction
                {
                    Id = expiredId,
                    FileName = "expired.pdf",
                    ContentType = "application/pdf",
                    FileSize = 256,
                    SuggestedDetails = suggestedJson,
                    Status = "Pending",
                    ExpiresAt = now.AddDays(-1),
                    CreatedAt = now,
                    UpdatedAt = now,
                    IsDeleted = false
                });

            await db.SaveChangesAsync();
        }

        await using (var db = await CreateScopeDbContextAsync())
        {
            // Mirror the expiry-job predicate: expiry marker matches Pending
            // rows whose window has lapsed; the JSON column is invisible to it.
            var expiring = await db.DocumentExtractions
                .AsNoTracking()
                .Where(x => x.Status == "Pending"
                    && x.ExpiresAt != null
                    && x.ExpiresAt < now)
                .Select(x => x.Id)
                .ToListAsync();

            Assert.DoesNotContain(pendingId, expiring);
            Assert.Contains(expiredId, expiring);

            // And the non-expired rows still resolve back to their JSON.
            var pending = await db.DocumentExtractions
                .AsNoTracking()
                .SingleAsync(x => x.Id == pendingId);

            AssertJsonEqual(suggestedJson, pending.SuggestedDetails);
            Assert.Equal("Pending", pending.Status);

            // PostgreSQL timestamptz stores microsecond precision; tolerate
            // sub-microsecond rounding of the written value.
            Assert.True(
                Math.Abs(
                    (pending.ExpiresAt!.Value - now.AddHours(1)).Ticks)
                    < TimeSpan.TicksPerMicrosecond,
                $"ExpiresAt drifted: {pending.ExpiresAt!.Value:O} vs {now.AddHours(1):O}");
        }
    }

    [Fact(DisplayName =
        "No OCR text is ever persisted on DocumentExtraction or its table")]
    public async Task NoOcrText_IsPersisted()
    {
        var extractionProperties = typeof(DocumentExtraction)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name)
            .ToList();

        Assert.DoesNotContain(extractionProperties, n =>
            n.Contains("Ocr", StringComparison.OrdinalIgnoreCase)
            || n.Contains("RawText", StringComparison.OrdinalIgnoreCase));

        await using var connection = new NpgsqlConnection(
            Fixture.Container.GetConnectionString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_name
            FROM information_schema.columns
            WHERE table_name = 'document_extractions';
            """;
        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        Assert.DoesNotContain(names, n =>
            n.Contains("ocr", StringComparison.OrdinalIgnoreCase)
            || n.Contains("raw_text", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("suggested_details", names);
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