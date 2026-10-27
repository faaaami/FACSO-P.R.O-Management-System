using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PROManagementSystem.Api.Common;
using PROManagementSystem.Application.Common.Interfaces;
using PROManagementSystem.Application.PaymentOrders.Commands.CompletePaymentFromWebhook;

namespace PROManagementSystem.Api.Controllers.Client;

/// <summary>
/// External Razorpay webhook callback. This is an anonymous business route
/// (not a Client-scoped action): authentication is provided solely by the
/// verified Razorpay webhook signature over the exact raw request body.
/// </summary>
[ApiController]
[Route("api/v1/client/payments")]
[AllowAnonymous]
public sealed class ClientPaymentsWebhookController : ControllerBase
{
    private const string SignatureHeader = "X-Razorpay-Signature";
    private const string EventIdHeader = "X-Razorpay-Event-Id";

    private readonly ISender _sender;
    private readonly IRazorpayService _razorpayService;

    public ClientPaymentsWebhookController(
        ISender sender,
        IRazorpayService razorpayService)
    {
        _sender = sender;
        _razorpayService = razorpayService;
    }

    [HttpPost("webhook")]
    [ProducesResponseType(
        typeof(ApiResponse<CompletePaymentFromWebhookResponseDto>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> HandleWebhook(
        CancellationToken cancellationToken)
    {
        // 1. Read the EXACT raw request body once. The same bytes are used for
        // both signature verification and JSON parsing - never deserialize and
        // reserialize between the two.
        var signature =
            Request.Headers[SignatureHeader].ToString();
        var eventId =
            Request.Headers[EventIdHeader].ToString();

        string rawBody;
        using (var reader = new StreamReader(
                   Request.Body,
                   Encoding.UTF8,
                   detectEncodingFromByteOrderMarks: false,
                   leaveOpen: true,
                   bufferSize: 8192))
        {
            rawBody = await reader.ReadToEndAsync(cancellationToken);
        }

        // 2. Verify the webhook signature (HMAC-SHA256 over raw body using
        // WebhookSecret). Fail closed on any invalid/missing signature or
        // missing secret. The raw body is never logged.
        var isValid = await _razorpayService
            .VerifyWebhookSignatureAsync(
                rawBody,
                signature,
                cancellationToken);

        if (!isValid)
        {
            return Unauthorized(
                ApiResponse<object>.FailureResponse(
                    "INVALID_WEBHOOK_SIGNATURE",
                    "Webhook signature verification failed."));
        }

        // 3. Parse the verified payload. From here on every outcome is 200:
        // a verified event is acknowledged even when it cannot be applied,
        // otherwise Razorpay retries it and eventually disables the webhook.
        // Only a body that is not JSON at all is refused - Razorpay never
        // signs one.
        RazorpayPaymentPayload payload;

        try
        {
            using var document =
                JsonDocument.Parse(rawBody);

            var root = document.RootElement;

            var eventName =
                root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("event", out var evt) &&
                evt.ValueKind == JsonValueKind.String
                    ? evt.GetString()
                    : null;

            // payment.* and order.paid events carry payload.payment.entity;
            // order.paid also carries payload.order.entity.
            var payment = TryGetEntity(root, "payment");
            var order = TryGetEntity(root, "order");

            payload = new RazorpayPaymentPayload(
                EventName: eventName ?? string.Empty,
                PaymentId: GetString(payment, "id"),
                OrderId: GetString(payment, "order_id") ?? GetString(order, "id"),
                AmountInSmallestUnit: GetInt(payment, "amount"),
                Currency: GetString(payment, "currency"),
                PaymentStatus: GetString(payment, "status"));
        }
        catch (JsonException)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "MALFORMED_WEBHOOK",
                    "Webhook payload is not valid JSON."));
        }

        // 4. Apply the event. The invoice is derived from the persisted
        // PaymentOrder keyed by order_id - never from arbitrary payload fields.
        var result = await _sender.Send(
            new CompletePaymentFromWebhookCommand(
                payload.OrderId,
                payload.PaymentId,
                payload.AmountInSmallestUnit,
                payload.Currency,
                payload.EventName,
                payload.PaymentStatus,
                eventId,
                signature),
            cancellationToken);

        return Ok(
            ApiResponse<CompletePaymentFromWebhookResponseDto>
                .SuccessResponse(result));
    }

    private static JsonElement? TryGetEntity(
        JsonElement root,
        string name)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty("payload", out var payloadEl) &&
               payloadEl.ValueKind == JsonValueKind.Object &&
               payloadEl.TryGetProperty(name, out var wrapper) &&
               wrapper.ValueKind == JsonValueKind.Object &&
               wrapper.TryGetProperty("entity", out var entity) &&
               entity.ValueKind == JsonValueKind.Object
            ? entity
            : null;
    }

    private static string? GetString(
        JsonElement? entity,
        string property)
    {
        return entity.HasValue &&
               entity.Value.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int? GetInt(
        JsonElement? entity,
        string property)
    {
        return entity.HasValue &&
               entity.Value.TryGetProperty(property, out var value) &&
               value.ValueKind == JsonValueKind.Number &&
               value.TryGetInt32(out var number)
            ? number
            : null;
    }

    private sealed record RazorpayPaymentPayload(
        string EventName,
        string? PaymentId,
        string? OrderId,
        int? AmountInSmallestUnit,
        string? Currency,
        string? PaymentStatus);
}
