import apiClient from '../axios'

/**
 * Admin payment-order endpoints. `[Authorize(Roles = "Admin")]` under /api/v1.
 *
 * INCONSISTENT DTO (verified): PaymentOrderDto declares `InvoiceType` and
 * `Status` as `int`, not as enums, so unlike every other Admin contract these
 * two fields arrive as NUMBERS (1..2 and 1..6). The global JsonStringEnumConverter
 * does not apply to them. See components/admin/enumLabels.js.
 *
 * Unlike the invoice DTOs, PaymentOrderDto DOES include `ClientCompanyName`, so
 * payment tables need no client-company map lookup.
 *
 * BOTH MUTATIONS ARE NOW WRAPPED, and both are one-way.
 *
 * Create calls the Razorpay gateway inside the handler, and cancel sets a local
 * status while leaving the gateway order open. Neither has a reversing endpoint in
 * the Admin API, so each is reached through the shared ConfirmDialog first. See
 * components/admin/invoices/billingDisplay.js for the shared confirmation copy and
 * hooks/admin/useAdminBillingMutations.js for the cache invalidation contract.
 */

/**
 * GET /api/v1/payment-orders
 * Query: clientCompanyId (Guid?), status (PaymentStatus?), page (1), pageSize (20)
 * item: PaymentOrderDto { id, invoiceType, retainerInvoiceId?, serviceFeeInvoiceId?,
 *   clientCompanyId, clientCompanyName, amount, currency, status, razorpayOrderId?,
 *   paidAt?, failedAt?, cancelledAt?, notes?, createdAt, updatedAt }
 */
export async function getAdminPaymentOrders(params = {}) {
  const response = await apiClient.get('/payment-orders', { params })
  return response.data.data
}

/** GET /api/v1/payment-orders/{id} */
export async function getAdminPaymentOrderById(id) {
  const response = await apiClient.get(`/payment-orders/${id}`)
  return response.data.data
}

/**
 * The `PaymentInvoiceType` enum's ORDINALS, as a name lookup.
 *
 * WHY THIS MAP EXISTS RATHER THAN SENDING THE NUMBER STRAIGHT THROUGH. The value
 * reaches this file as the ordinal the read DTOs use (`PaymentOrderDto.InvoiceType`
 * is `int`), and ASP.NET Core's `JsonStringEnumConverter` would in fact still bind an
 * integer — `allowIntegerValues` defaults to true. So sending `1` would WORK today.
 *
 * It is mapped anyway, for two reasons, and neither is style. First, the documented
 * contract for this endpoint is the enum NAME, and a wrapper that silently relies on
 * a permissive converter default to be correct is a wrapper that breaks silently if
 * anyone ever tightens that converter or swaps the serializer — it would surface as a
 * 400 from the server, not as a failing test. Second, it makes the payload readable
 * in the network tab as the thing it is, rather than as `1`, which is exactly the
 * ambiguity this module already suffers from on the response side.
 *
 * An ordinal that is not in this map is a programming error, not a server error, and
 * is passed through unchanged rather than being guessed at: `undefined` would be sent
 * as a missing property and the server would answer with its own message about the
 * enum, which is more useful than a client-side guess.
 */
const PAYMENT_INVOICE_TYPE_NAMES = {
  1: 'RetainerInvoice',
  2: 'ServiceFeeInvoice',
}

/* -------------------------------------------------------------------------- */
/*  MUTATIONS                                                                  */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/payment-orders -> 201
 *
 * CreatePaymentOrderRequest(InvoiceType, RetainerInvoiceId, ServiceFeeInvoiceId, Notes)
 *
 * FOUR FIELDS, AND ONLY FOUR. There is deliberately no amount, no currency, no
 * clientCompanyId and no capture or settlement field, because the request record has
 * no such properties: CreatePaymentOrderCommandHandler copies Amount and Currency
 * off the referenced invoice and takes ClientCompanyId from that invoice's owner.
 * A payload carrying any of those would be a field the server does not bind.
 *
 * `InvoiceType` is the `PaymentInvoiceType` enum (RetainerInvoice = 1,
 * ServiceFeeInvoice = 2) and is sent as its NAME, since Program.cs registers a
 * global JsonStringEnumConverter. NOTE THE INCONSISTENCY THIS CREATES: the READ
 * DTOs declare `InvoiceType` as `int` so they arrive as numbers, but this RESPONSE
 * DTO declares it as the enum, so the create response's `invoiceType` arrives as a
 * string. Nothing in this module reads the type off the create response — the order
 * is re-read through the invalidated list/detail keys — precisely because the two
 * wire forms do not match.
 *
 * EXACTLY ONE INVOICE ID, AND IT MUST MATCH THE TYPE:
 *   - RetainerInvoice    requires RetainerInvoiceId, 409 otherwise
 *   - ServiceFeeInvoice  requires ServiceFeeInvoiceId, 409 otherwise
 * So this wrapper sends the id for the chosen type and leaves the other column
 * `null` rather than omitting it, which keeps the payload a complete picture of the
 * request record and can never present both columns.
 *
 * SERVER RULES, in the order the handler applies them:
 *   - the invoice must exist and not be deleted   -> 404
 *   - it must belong to the caller                -> 403 (Admin is exempt)
 *   - it must be Pending                          -> 409 "Only pending invoices can
 *                                                    be paid"
 *   - an order for it must not already carry a
 *     RazorpayOrderId                            -> 409 "A payment order already
 *                                                    exists for this invoice"
 *   - Notes <= 5000 characters                    -> 400
 *
 * A pre-existing order with NO RazorpayOrderId is not a conflict: the handler takes
 * a retry path, raises a fresh gateway order against the same record and writes a
 * `PaymentOrderRetried` audit row. That is why the duplicate guard tests the gateway
 * id and not the status.
 *
 * THE RESPONSE CARRIES GATEWAY DATA the request never sent: `razorpayOrderId`,
 * `razorpayAmount` (the amount in the gateway's minor units — the handler multiplies
 * by 100) and `razorpayKeyId`. The order is returned in `Pending` once the gateway
 * has issued an id.
 *
 * THIS DOES NOT SETTLE THE INVOICE. The order is created and left Pending for the
 * client to pay through Razorpay; only the gateway webhook, or an explicit invoice
 * mark-paid, moves the invoice to Paid.
 */
export async function createAdminPaymentOrder(payload) {
  const response = await apiClient.post('/payment-orders', {
    // Mapped to the enum NAME, per the contract above. `?? payload.invoiceType` keeps
    // an already-correct string working, so a caller that has a name rather than an
    // ordinal is not broken by this.
    invoiceType: PAYMENT_INVOICE_TYPE_NAMES[payload.invoiceType] ?? payload.invoiceType,
    retainerInvoiceId: payload.retainerInvoiceId ?? null,
    serviceFeeInvoiceId: payload.serviceFeeInvoiceId ?? null,
    notes: payload.notes ? payload.notes : null,
  })
  return response.data.data
}

/**
 * PATCH /api/v1/payment-orders/{id}/cancel -> 200
 *
 * NO BODY IS SENT, because the controller action takes no body parameter at all —
 * only the route id. The `CancelPaymentOrderRequest` record that exists on the
 * command side has no properties, and the route does not bind it.
 *
 * SERVER RULES:
 *   - the order must exist and not be deleted  -> 404
 *   - it must belong to the caller             -> 403 (Admin is exempt)
 *   - it must be `Created` (1) or `Pending` (2) -> 409 "Cannot cancel a payment
 *     order in {status} status. Only Created or Pending orders can be cancelled."
 *
 * WHAT THIS ACTUALLY DOES, precisely, because it is narrower than "cancels the
 * payment": CancelPaymentOrderCommandHandler sets Status = Cancelled and CancelledAt
 * and writes a `PaymentOrderCancelled` audit row. It does NOT call the gateway, does
 * NOT clear or close `RazorpayOrderId`, and does NOT touch the linked invoice — so a
 * Pending invoice stays Pending and remains unpaid.
 *
 * The consequence that outlives the click: because the gateway reference is kept,
 * the duplicate guard in the create handler still matches, so that invoice can never
 * be given a new payment order through this path. And because the gateway order is
 * left open, a client who still pays it settles at the gateway while this record
 * reads Cancelled.
 */
export async function cancelAdminPaymentOrder(id) {
  const response = await apiClient.patch(`/payment-orders/${id}/cancel`)
  return response.data.data
}
