import apiClient from '../axios'

/**
 * Admin invoice endpoints. `[Authorize(Roles = "Admin")]` under /api/v1.
 *
 * Four separate resources, each with its own status enum and its own DTO shape.
 * RetainerInvoiceStatus / ServiceFeeInvoiceStatus are Pending=1, Paid=2, Void=3 and
 * arrive string-serialized. GovFeeDisbursementStatus is PaidByFirm=1,
 * InvoicedToClient=2, Reimbursed=3 and is also string-serialized. Payment Orders
 * live in a separate wrapper (api/admin/payments.js) and are the ONE exception:
 * PaymentOrderDto declares InvoiceType and Status as `int`, so those two arrive as
 * numbers.
 *
 * MUTATIONS ARE NOW WRAPPED, and every one of them is one-way.
 *
 * Eight writes live below: three per invoice type (create, mark-paid, void) and two
 * for government fees (create, status advance). None of the eight has a reversing
 * endpoint anywhere in the Admin API — no unvoid, no un-pay, no status rollback —
 * so each is reached through the shared ConfirmDialog first. See
 * components/admin/invoices/billingDisplay.js for the shared confirmation copy and
 * hooks/admin/useAdminBillingMutations.js for the cache invalidation contract.
 *
 * TWO RESPONSE SHAPE FACTS THAT AFFECT THE CALLERS, both read off the handlers:
 *   - Mark-paid changes the linked PaymentOrder as well as the invoice, in the same
 *     transaction (MarkRetainerInvoicePaidCommandHandler step 7), and NEITHER
 *     invoice detail DTO carries the linked order's id, so a caller cannot target
 *     that order's cache entry by id and must invalidate at the prefix instead.
 *   - CreateServiceFeeInvoiceResponseDto is produced by the creation service, which
 *     requires the renewal task to be `Updated`; see the gating note on that route.
 */

/**
 * GET /api/v1/retainer-invoices
 * Query: clientCompanyId (Guid?), serviceContractId (Guid?), status (RetainerInvoiceStatus?),
 *   page (1), pageSize (20)
 * item: GetRetainerInvoiceListItemDto { id, clientCompanyId, serviceContractId,
 *   invoiceNumber, invoiceDate, dueDate, periodStart?, periodEnd?, amount, currency,
 *   status, notes?, paidAt?, voidedAt?, voidReason?, createdAt }
 * Note: no company name is included — resolve it from the client company map.
 */
export async function getAdminRetainerInvoices(params = {}) {
  const response = await apiClient.get('/retainer-invoices', { params })
  return response.data.data
}

/** GET /api/v1/retainer-invoices/{id} */
export async function getAdminRetainerInvoiceById(id) {
  const response = await apiClient.get(`/retainer-invoices/${id}`)
  return response.data.data
}

/**
 * GET /api/v1/retainer-invoices/{id}/pdf
 *
 * RETURNS A RAW FILE, NOT AN ApiResponse ENVELOPE. The controller writes
 * `return File(pdfBytes, "application/pdf", fileName)`, so the body is the PDF
 * itself — there is no `data` property to unwrap. This wrapper therefore returns
 * the whole Axios response, and a caller that reached for `response.data.data`
 * would get undefined.
 *
 * `responseType: 'blob'` is required: without it Axios treats the body as text and
 * mangles binary content. It also means a failed request arrives as a Blob rather
 * than a parsed object, so the download button reads the blob's text to recover the
 * server's error message instead of assuming `error.response.data.message` exists.
 */
export async function getAdminRetainerInvoicePdf(id) {
  return apiClient.get(`/retainer-invoices/${id}/pdf`, { responseType: 'blob' })
}

/**
 * GET /api/v1/service-fee-invoices
 * Query: clientCompanyId (Guid?), renewalTaskId (Guid?), status (ServiceFeeInvoiceStatus?),
 *   page (1), pageSize (20)
 */
export async function getAdminServiceFeeInvoices(params = {}) {
  const response = await apiClient.get('/service-fee-invoices', { params })
  return response.data.data
}

/** GET /api/v1/service-fee-invoices/{id} */
export async function getAdminServiceFeeInvoiceById(id) {
  const response = await apiClient.get(`/service-fee-invoices/${id}`)
  return response.data.data
}

/**
 * GET /api/v1/service-fee-invoices/{id}/pdf
 *
 * Raw file response, exactly as the retainer PDF: `File(...)` with no ApiResponse
 * envelope, so the whole Axios response is returned and `response.data.data` does
 * not exist. `responseType: 'blob'` for the same binary reason.
 */
export async function getAdminServiceFeeInvoicePdf(id) {
  return apiClient.get(`/service-fee-invoices/${id}/pdf`, { responseType: 'blob' })
}

/**
 * GET /api/v1/gov-fee-disbursements
 * Query: clientCompanyId (Guid?), status (GovFeeDisbursementStatus?), page (1), pageSize (20)
 *
 * THERE IS NO PDF ROUTE FOR THIS RESOURCE, unlike the two invoice types above: the
 * controller exposes only list, getById, create and status-change. No download
 * control may be offered for a government fee, because there is nothing to call.
 *
 * There is also no `renewalTaskId` parameter here, even though the DTO carries a
 * nullable one — so a task filter may not be offered on this tab.
 */
export async function getAdminGovFeeDisbursements(params = {}) {
  const response = await apiClient.get('/gov-fee-disbursements', { params })
  return response.data.data
}

/** GET /api/v1/gov-fee-disbursements/{id} */
export async function getAdminGovFeeDisbursementById(id) {
  const response = await apiClient.get(`/gov-fee-disbursements/${id}`)
  return response.data.data
}

/* -------------------------------------------------------------------------- */
/*  MUTATIONS                                                                  */
/*                                                                             */
/*  Every route below is one-way. The request bodies are transcribed field-for-  */
/*  field from the *Request records the controller binds, and the rules beside   */
/*  each one are the validator and handler rules that a caller has to know       */
/*  before it can fill the form — not new rules.                                 */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/retainer-invoices -> 201
 *
 * CreateRetainerInvoiceRequest(
 *   ClientCompanyId, ServiceContractId, InvoiceNumber,
 *   InvoiceDate, DueDate, PeriodStart, PeriodEnd,
 *   Amount, Currency, Notes)
 *
 * `PeriodStart` and `PeriodEnd` are `DateTime?` and `Notes` is `string?`, so they
 * are always SENT — as null when the field is left empty — rather than omitted.
 * CreateRetainerInvoiceCommandHandler assigns each of them straight onto the
 * entity, so an omitted key and an explicit null are equivalent here; sending them
 * explicitly keeps the payload a complete picture of the request record.
 *
 * PROPERTY NAMES ARE camelCase, matching the server's own wire form:
 * `AddControllers()` applies the Web defaults, which camelCase both directions, so
 * `clientCompanyId` is what binds to the record's `ClientCompanyId`. Every wrapper
 * in this file speaks that casing; the PascalCase names in the comment above are
 * the C# record's, not the JSON's.
 *
 * SERVER RULES this form exists to satisfy:
 *   - the company must exist and not be deleted              -> 404 otherwise
 *   - the contract must exist and not be deleted              -> 404 otherwise
 *   - the contract must belong to the company                 -> 409 "does not belong"
 *   - the contract must be Active                             -> 409 "must be active"
 *   - the invoice number must be unique across non-deleted    -> 409 "already exists"
 *   - DueDate >= InvoiceDate                                 -> 400
 *   - PeriodEnd >= PeriodStart, when both are present        -> 400
 *   - InvoiceNumber 1..100, Amount >= 0, Currency non-empty   -> 400
 *
 * The contract is therefore NOT free choice: the handler accepts only the one
 * Active contract, so the caller resolves it with getAdminActiveContract.
 *
 * New invoices start in `Pending` — the handler assigns it; the request has no
 * status field and none is sent.
 */
export async function createAdminRetainerInvoice(payload) {
  const response = await apiClient.post('/retainer-invoices', {
    clientCompanyId: payload.clientCompanyId,
    serviceContractId: payload.serviceContractId,
    invoiceNumber: payload.invoiceNumber,
    invoiceDate: payload.invoiceDate,
    dueDate: payload.dueDate,
    periodStart: payload.periodStart ?? null,
    periodEnd: payload.periodEnd ?? null,
    amount: payload.amount,
    currency: payload.currency,
    notes: payload.notes ?? null,
  })
  return response.data.data
}

/**
 * PATCH /api/v1/retainer-invoices/{id}/mark-paid -> 200
 *
 * NO BODY IS SENT, because the controller action takes no body parameter at all —
 * only the route id. Sending one would be a payload the route does not bind.
 *
 * SERVER RULES:
 *   - the invoice must exist and not be deleted  -> 404 otherwise
 *   - it must be Pending                         -> 409 "Only pending invoices can be
 *                                                  marked as paid"
 *
 * SIDE EFFECT THE CALLER MUST KNOW: in the same transaction the handler also sets
 * the linked PaymentOrder — if one exists — to Paid and stamps its PaidAt, locking
 * the order row first to match the webhook's lock order. Mark-paid is therefore
 * never a single-record change, and the invoice detail DTO carries no
 * `paymentOrderId` with which to target that order's cache entry.
 *
 * The handler is idempotent-by-conflict rather than idempotent-by-success: a second
 * call on an already-paid invoice is a 409, not a no-op.
 */
export async function markAdminRetainerInvoicePaid(id) {
  const response = await apiClient.patch(`/retainer-invoices/${id}/mark-paid`)
  return response.data.data
}

/**
 * PATCH /api/v1/retainer-invoices/{id}/void -> 200
 *
 * VoidRetainerInvoiceRequest(string? Reason) — a single nullable field, so the body
 * carries exactly one property. `reason` is OPTIONAL on the server:
 * VoidRetainerInvoiceCommandValidator has no rule for it and the handler stores
 * `request.Reason?.Trim()`, so an empty reason is stored as null. The body is sent
 * either way, because the controller action does declare the request parameter.
 *
 * SERVER RULES:
 *   - the invoice must exist and not be deleted  -> 404 otherwise
 *   - it must be Pending                         -> 409 "Only pending invoices can be
 *                                                  voided"
 *
 * Void is terminal: there is no route that returns a voided invoice to Pending, so
 * a void can never be paid and can never be reopened.
 */
export async function voidAdminRetainerInvoice(id, reason) {
  const response = await apiClient.patch(`/retainer-invoices/${id}/void`, {
    reason: reason ? reason : null,
  })
  return response.data.data
}

/**
 * POST /api/v1/service-fee-invoices -> 201
 *
 * CreateServiceFeeInvoiceRequest(
 *   ClientCompanyId, RenewalTaskId, InvoiceNumber,
 *   InvoiceDate, DueDate, Amount, Currency, Description)
 *
 * NOTE THE SHAPE, because it is NOT the retainer shape: this one has NO period
 * fields, and `Description` is the OPTIONAL extra rather than a substitute for the
 * invoice number. InvoiceNumber, InvoiceDate and DueDate are all required here,
 * exactly as on the retainer — a Service Fee invoice is a single dated charge
 * rather than a period, not an unnumbered one. GetServiceFeeInvoiceListItemDto
 * carries an invoiceNumber too, so a Service Fee invoice is numbered and dated in
 * the same way.
 *
 * ONLY `Description` is nullable; the two trailing `string?`/nullable slots are
 * `Description` alone. Every other field is sent explicitly.
 *
 * SERVER RULES (ServiceFeeInvoiceCreationService, which the command handler
 * delegates to wholesale):
 *   - the company must exist and not be deleted   -> 404 "Client company was not found"
 *   - the task must exist and not be deleted       -> 404 "Renewal task was not found"
 *   - the task must belong to the company          -> 409 "does not belong"
 *   - the task must be `Updated`                    -> 409 "must be completed (Updated)"
 *   - the invoice number must be unique             -> 409 "already exists"
 *   - the task must not already have an invoice     -> 409 "already exists for this
 *                                                     renewal task"
 *   - DueDate >= InvoiceDate, InvoiceNumber 1..100, Amount >= 0, Currency non-empty
 *
 * The `Updated` requirement is the gate the UI checks before offering submission.
 * New invoices start `Pending`, assigned by the service.
 */
export async function createAdminServiceFeeInvoice(payload) {
  const response = await apiClient.post('/service-fee-invoices', {
    clientCompanyId: payload.clientCompanyId,
    renewalTaskId: payload.renewalTaskId,
    invoiceNumber: payload.invoiceNumber,
    invoiceDate: payload.invoiceDate,
    dueDate: payload.dueDate,
    amount: payload.amount,
    currency: payload.currency,
    description: payload.description ? payload.description : null,
  })
  return response.data.data
}

/**
 * PATCH /api/v1/service-fee-invoices/{id}/mark-paid -> 200 — no body, as above.
 *
 * SERVER RULES: 404 if absent, 409 unless `Pending`.
 *
 * Like its retainer twin, MarkServiceFeeInvoicePaidCommandHandler also sets the
 * linked PaymentOrder to Paid in the same transaction, and
 * GetServiceFeeInvoiceByIdResponseDto carries no paymentOrderId either.
 */
export async function markAdminServiceFeeInvoicePaid(id) {
  const response = await apiClient.patch(`/service-fee-invoices/${id}/mark-paid`)
  return response.data.data
}

/**
 * PATCH /api/v1/service-fee-invoices/{id}/void -> 200
 *
 * VoidServiceFeeInvoiceRequest(string? Reason) — identical shape and identical
 * optionality to the retainer's: no validator rule, stored as `Reason?.Trim()`.
 * 404 if absent, 409 unless `Pending`, and terminal once applied.
 */
export async function voidAdminServiceFeeInvoice(id, reason) {
  const response = await apiClient.patch(`/service-fee-invoices/${id}/void`, {
    reason: reason ? reason : null,
  })
  return response.data.data
}

/**
 * POST /api/v1/gov-fee-disbursements -> 201
 *
 * CreateGovFeeDisbursementRequest(
 *   ClientCompanyId, RenewalTaskId, FeeDescription,
 *   GovernmentReference, Amount, Currency, Notes)
 *
 * A GOVERNMENT FEE IS NOT AN INVOICE, and the request proves it: there is no
 * InvoiceNumber, no InvoiceDate and no DueDate, because a disbursement is the firm
 * having paid a government fee and waiting to be reimbursed rather than a charge
 * against a client. `RenewalTaskId` is `Guid?` and is the only optional reference.
 *
 * SERVER RULES: client company required, FeeDescription 1..2000, Amount >= 0,
 * Currency non-empty — all 400 when breached. There is no validator rule for
 * RenewalTaskId, GovernmentReference or Notes, so none is invented here.
 *
 * `PaidByFirmAt` is stamped by the handler and is not a request field. A new
 * disbursement starts at `PaidByFirm`, the first status in the forward-only chain.
 */
export async function createAdminGovFeeDisbursement(payload) {
  const response = await apiClient.post('/gov-fee-disbursements', {
    clientCompanyId: payload.clientCompanyId,
    renewalTaskId: payload.renewalTaskId ?? null,
    feeDescription: payload.feeDescription,
    governmentReference: payload.governmentReference ?? null,
    amount: payload.amount,
    currency: payload.currency,
    notes: payload.notes ?? null,
  })
  return response.data.data
}

/**
 * PATCH /api/v1/gov-fee-disbursements/{id}/status -> 200
 *
 * UpdateGovFeeDisbursementStatusRequest(GovFeeDisbursementStatus Status) — ONE
 * field. There is no validator on this command at all: the guard is the handler's
 * switch, which permits exactly one target per current status —
 *
 *   PaidByFirm       -> only InvoicedToClient   (stamps InvoicedAt)
 *   InvoicedToClient -> only Reimbursed         (stamps ReimbursedAt)
 *   Reimbursed       -> 409 "already been reimbursed"
 *   any other target -> 409
 *
 * The status is sent as its enum NAME because Program.cs registers a global
 * JsonStringEnumConverter, so the name is the wire form the server binds.
 */
export async function updateAdminGovFeeDisbursementStatus(id, status) {
  const response = await apiClient.patch(`/gov-fee-disbursements/${id}/status`, {
    status,
  })
  return response.data.data
}
