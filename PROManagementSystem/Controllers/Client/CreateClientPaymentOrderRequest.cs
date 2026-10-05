namespace PROManagementSystem.Api.Controllers.Client;

/// <summary>
/// Invoice types accepted by the client create-order endpoint. Kept separate
/// from the domain <c>PaymentInvoiceType</c> so the client-facing payload uses
/// the compact <c>Retainer</c>/<c>ServiceFee</c> tokens required by the Day 4
/// specification, then mapped to the existing command.
/// </summary>
public enum ClientPaymentInvoiceType
{
    Retainer,
    ServiceFee
}

/// <summary>
/// Body for POST /api/v1/client/payments/create-order.
/// The <see cref="InvoiceType"/> names the invoice kind and <see cref="InvoiceId"/>
/// identifies the (owned) invoice. No company identifier is accepted - the tenant
/// is always derived from the authenticated client and never from this payload.
/// </summary>
public sealed record CreateClientPaymentOrderRequest(
    ClientPaymentInvoiceType? InvoiceType,
    Guid InvoiceId);
