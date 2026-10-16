using System.Net;
using System.Net.Http.Headers;
using ImageMagick;
using PROManagementSystem.Tests.Infrastructure;
using Xunit;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// iPhone HEIC photos are accepted at upload and stored as JPEG so they can
/// be previewed and downloaded like any other photo.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class HeicUploadApiTests : IntegrationTestBase
{
    public HeicUploadApiTests(DatabaseFixture fixture)
        : base(fixture)
    {
    }

    private static HttpRequestMessage Upload(string contentType, string fileName, byte[] bytes)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        var form = new MultipartFormDataContent();
        form.Add(file, "file", fileName);
        form.Add(new StringContent(nameof(Domain.Enums.DocumentType.Passport)), "documentType");

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/documents/extract")
        {
            Content = form
        };

        request.Headers.Add(
            "X-Test-Identity",
            TestIdentityService.HeaderPayload(TestDataSeeder.Ids.Admin, "Admin", null));

        return request;
    }

    private async Task<(Guid ExtractionId, string ContentType, string FileName)> StoredAsync(HttpResponseMessage response)
    {
        await EnsureOk(response, HttpStatusCode.OK);

        var json = System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var id = json["data"]!["extractionId"]!.GetValue<Guid>();

        await using var db = await CreateScopeDbContextAsync();
        var row = db.DocumentExtractions.Single(x => x.Id == id);

        return (id, row.ContentType, row.FileName);
    }

    [Theory(DisplayName = "A HEIC upload is stored and downloadable as JPEG")]
    [InlineData("image/heic", "IMG_0042.HEIC")]
    [InlineData("application/octet-stream", "IMG_0042.heic")]
    public async Task Heic_IsStoredAsJpeg(string declaredType, string fileName)
    {
        var response = await ScopeClient.SendAsync(
            Upload(declaredType, fileName, HeicFixture.Bytes()));

        var (id, contentType, storedName) = await StoredAsync(response);

        Assert.Equal("image/jpeg", contentType);
        Assert.Equal("IMG_0042.jpg", storedName);

        var download = await ScopeClient.SendAsync(
            ScopeGet($"/api/v1/admin/documents/extractions/{id}/file", TestDataSeeder.Ids.Admin, "Admin"));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/jpeg", download.Content.Headers.ContentType?.MediaType);

        using var image = new MagickImage(await download.Content.ReadAsByteArrayAsync());
        Assert.Equal(MagickFormat.Jpeg, image.Format);
        Assert.Equal((uint)HeicFixture.Width, image.Width);
    }

    [Fact(DisplayName = "A file renamed to .heic that is not HEIC is rejected")]
    public async Task FakeHeic_IsRejected()
    {
        var response = await ScopeClient.SendAsync(
            Upload("image/heic", "not-really.heic", "%PDF-1.4\n%%EOF\n"u8.ToArray()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
