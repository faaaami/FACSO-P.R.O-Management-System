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
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
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

        // 3. Parse the verified payload.
        RazorpayPaymentPayload? payload;

        try
        {
            using var document =
                JsonDocument.Parse(rawBody);

            if (!document.RootElement.TryGetProperty(
                    "payload", out var payloadEl) ||
                !payloadEl.TryGetProperty(
                    "payment", out var paymentEl) ||
                !paymentEl.TryGetProperty(
                    "entity", out var entity))
            {
                return BadRequest(
                    ApiResponse<object>.FailureResponse(
                        "MALFORMED_WEBHOOK",
                        "Webhook payload is missing payment entity."));
            }

            var eventName =
                document.RootElement.TryGetProperty("event", out var evt)
                    ? evt.GetString()
                    : null;

            var id = entity.TryGetProperty("id", out var idEl)
                ? idEl.GetString()
                : null;
            var orderId = entity.TryGetProperty("order_id", out var orderEl)
                ? orderEl.GetString()
                : null;
            var currency = entity.TryGetProperty("currency", out var curEl)
                ? curEl.GetString()
                : null;
            var status = entity.TryGetProperty("status", out var stEl)
                ? stEl.GetString()
                : null;

            var amount = entity.TryGetProperty("amount", out var amtEl) &&
                         amtEl.TryGetInt32(out var amt)
                ? amt
                : (int?)null;

            payload = new RazorpayPaymentPayload(
                EventName: eventName ?? string.Empty,
                PaymentId: id ?? string.Empty,
                OrderId: orderId ?? string.Empty,
                AmountInSmallestUnit: amount,
                Currency: currency ?? string.Empty,
                PaymentStatus: status ?? string.Empty);
        }
        catch (JsonException)
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "MALFORMED_WEBHOOK",
                    "Webhook payload is not valid JSON."));
        }

        // 4. Validate required parsed fields.
        if (payload is null ||
            string.IsNullOrWhiteSpace(payload.PaymentId) ||
            string.IsNullOrWhiteSpace(payload.OrderId) ||
            !payload.AmountInSmallestUnit.HasValue ||
            string.IsNullOrWhiteSpace(payload.Currency) ||
            string.IsNullOrWhiteSpace(payload.EventName) ||
            string.IsNullOrWhiteSpace(payload.PaymentStatus))
        {
            return BadRequest(
                ApiResponse<object>.FailureResponse(
                    "MALFORMED_WEBHOOK",
                    "Webhook payload is missing required fields."));
        }

        // 5. Complete the payment. The invoice is derived from the persisted
        // PaymentOrder keyed by order_id - never from arbitrary payload fields.
        var result = await _sender.Send(
            new CompletePaymentFromWebhookCommand(
                payload.OrderId,
                payload.PaymentId,
                payload.AmountInSmallestUnit.Value,
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

    private sealed record RazorpayPaymentPayload(
        string EventName,
        string PaymentId,
        string OrderId,
        int? AmountInSmallestUnit,
        string Currency,
        string PaymentStatus);
}
