using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace PROManagementSystem.Tests.Extraction;

/// <summary>
/// Task 7C.3.1: a deterministic fake <see cref="HttpMessageHandler"/> for the
/// OpenRouter HTTP-foundation tests. It records every outbound request (method,
/// URI, bearer header, body) and returns a canned response — or invokes a
/// supplied responder. No real network call is ever made.
/// </summary>
internal sealed class RecordingHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
        _responder;

    private readonly List<RecordedRequest> _requests = new();

    public RecordingHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>>
            responder)
    {
        _responder = responder;
    }

    public RecordingHttpMessageHandler(
        HttpStatusCode statusCode,
        string? jsonContent)
        : this((_, _) => Task.FromResult(BuildResponse(statusCode, jsonContent)))
    {
    }

    public IReadOnlyList<RecordedRequest> Requests => _requests;

    public int RequestCount => _requests.Count;

    public RecordedRequest LastRequest => _requests[^1];

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        string? body = null;

        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }

        _requests.Add(
            new RecordedRequest(
                request.Method,
                request.RequestUri,
                request.Headers.Authorization,
                request.Content?.Headers.ContentType?.MediaType,
                body));

        return await _responder(request, cancellationToken);
    }

    private static HttpResponseMessage BuildResponse(
        HttpStatusCode statusCode,
        string? jsonContent)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                jsonContent ?? string.Empty,
                Encoding.UTF8,
                "application/json")
        };
    }

    internal sealed record RecordedRequest(
        HttpMethod Method,
        Uri? RequestUri,
        AuthenticationHeaderValue? Authorization,
        string? ContentType,
        string? Body);
}