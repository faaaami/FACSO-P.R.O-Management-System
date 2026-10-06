import { CreditCard, FileText, Landmark, Receipt } from 'lucide-react'
import {
  GOV_FEE_DISBURSEMENT_STATUS,
  INVOICE_STATUS_VALUES,
  PAYMENT_INVOICE_TYPES,
  PAYMENT_STATUS,
  RETAINER_INVOICE_STATUS,
  SERVICE_FEE_INVOICE_STATUS,
  enumLabel,
  paymentInvoiceTypeLabel,
  paymentStatusLabel,
  retainerInvoiceStatusLabel,
  serviceFeeInvoiceStatusLabel,
} from '../enumLabels'
import { MISSING_VALUE, presentText, truncate } from '../clients/clientDisplay'
import { formatDate, formatDateTime, formatMoney } from '../../client/billing/format'

/**
 * Presentation vocabulary for the Admin Invoices / Billing module.
 *
 * Every field name referenced anywhere in this module was read out of the backend
 * records, not inferred:
 *
 *   GetRetainerInvoiceListItemDto   id, clientCompanyId, serviceContractId,
 *                                   invoiceNumber, invoiceDate, dueDate,
 *                                   periodStart?, periodEnd?, amount, currency,
 *                                   status, notes?, paidAt?, voidedAt?,
 *                                   voidReason?, createdAt
 *   GetRetainerInvoiceByIdResponseDto
 *                                   the above + clientCompanyName, contractNumber,
 *                                   updatedAt — and NOTHING else
 *   GetServiceFeeInvoiceListItemDto id, clientCompanyId, renewalTaskId,
 *                                   invoiceNumber, invoiceDate, dueDate, amount,
 *                                   currency, status, description?, paidAt?,
 *                                   voidedAt?, voidReason?, createdAt
 *   GetServiceFeeInvoiceByIdResponseDto
 *                                   the above + clientCompanyName, updatedAt
 *   GovFeeDisbursementDto           id, clientCompanyId, clientCompanyName,
 *                                   renewalTaskId?, feeDescription,
 *                                   governmentReference?, amount, currency,
 *                                   status, paidByFirmAt, invoicedAt?,
 *                                   reimbursedAt?, notes?, createdAt, updatedAt
 *   GetGovFeeDisbursementByIdResponseDto
 *                                   IDENTICAL to GovFeeDisbursementDto. The list and
 *                                   the detail are the same record; the detail is still
 *                                   fetched on open because it is the authoritative
 *                                   read and may diverge from a cached list row.
 *   PaymentOrderDto                 id, invoiceType (int), retainerInvoiceId?,
 *                                   serviceFeeInvoiceId?, clientCompanyId,
 *                                   clientCompanyName, amount, currency, status (int),
 *                                   razorpayOrderId?, paidAt?, failedAt?,
 *                                   cancelledAt?, notes?, createdAt, updatedAt
 *   GetPaymentOrderByIdResponseDto  the above + razorpayPaymentId
 *
 * THREE DTO FAMILIES, NOT ONE. Retainer, Service Fee, Government Fee and Payment
 * Order each have their own status enum, their own narrative field and their own
 * vocabulary. A shared "billing" tone map keyed by one set of statuses would be a
 * lie for the other three, so the four maps below are separate and local. Nothing
 * here restates a shared field between two different enums.
 *
 * ------------------------------------------------------------------
 * NUMERIC ENUMS ARE NOT COERCED.
 * ------------------------------------------------------------------
 * `PROManagementSystem/Program.cs` registers a global JsonStringEnumConverter, so
 * every enum-typed DTO property arrives as its NAME. PaymentOrderDto.InvoiceType
 * and PaymentOrderDto.Status are declared `int` on the DTO itself, so those two
 * arrive as NUMBERS (1..2 and 1..6) and the converter does not apply to them.
 * Their maps are therefore keyed numerically and are looked up WITHOUT
 * String() coercion, so a value that arrives as a number is never turned into a
 * key that does not exist, and a value that arrives as a string is not silently
 * "fixed" into one. If the backend ever changes the wire type, the lookup misses
 * and the raw value is shown — which is a visible contract change rather than a
 * silent mislabel.
 */

/** Re-exported canonical primitives — see the module note above. */
export { MISSING_VALUE, presentText, truncate }
export { formatDate, formatDateTime, formatMoney }

/**
 * The three invoice statuses in enum-ordinal order, derived from the verified
 * ordinals rather than written out, so any future ordering uses the enum's own.
 * Retainer and Service Fee share these ordinals (Pending=1, Paid=2, Void=3) but
 * remain two separate enums; the ORDER is the only thing they have in common.
 */
export const INVOICE_STATUS_KEYS = Object.keys(INVOICE_STATUS_VALUES).sort(
  (a, b) => INVOICE_STATUS_VALUES[a] - INVOICE_STATUS_VALUES[b],
)

/**
 * GovFeeDisbursementStatus — PaidByFirm=1, InvoicedToClient=2, Reimbursed=3.
 *
 * The LABELS come from the shared admin enumLabels map, but the ORDINALS are kept
 * LOCAL to this module: enumLabels.js has no GOV_FEE_DISBURSEMENT_STATUS_VALUES
 * record, and adding one there would mean editing a completed, protected file for
 * no other module's benefit. They live here instead, next to the tone map that
 * uses them, so the two are read together and cannot drift.
 */
export const GOV_FEE_STATUS_VALUES = {
  PaidByFirm: 1,
  InvoicedToClient: 2,
  Reimbursed: 3,
}

export const GOV_FEE_STATUS_KEYS = Object.keys(GOV_FEE_STATUS_VALUES).sort(
  (a, b) => GOV_FEE_STATUS_VALUES[a] - GOV_FEE_STATUS_VALUES[b],
)

/**
 * APPROVED DESIGN.md TONE MAPS — four local maps, one per status family.
 *
 *   Retainer / Service Fee   Pending -> warning, Paid -> success, Void -> neutral
 *   Government Fee           PaidByFirm / InvoicedToClient -> warning,
 *                            Reimbursed -> success
 *   Payment Order            Created -> neutral, Pending -> warning,
 *                            Paid -> success, Failed -> danger,
 *                            Cancelled / Refunded -> neutral
 *
 * `Void` and `Cancelled` are NEUTRAL rather than danger on purpose: a voided
 * invoice and a cancelled payment order are normal, deliberate terminal records
 * and not failures. Colouring them red would imply the platform had a problem.
 *
 * A GOVERNMENT FEE INVOICED TO THE CLIENT IS `warning`, not `success`, because
 * the firm is still out of pocket until the client reimburses it — only
 * Reimbursed means the money is back.
 *
 * Colour is decorative only. Every status ships its text label through StatusPill
 * or an adjacent text node, so status is never communicated by colour alone.
 *
 * An UNRECOGNISED member falls through to 'warning' rather than to a healthy
 * tone, so a status the backend adds later stays visibly "needs attention"
 * instead of silently inheriting a Paid/Reimbursed palette.
 */
export const INVOICE_STATUS_TONE = {
  Pending: 'warning',
  Paid: 'success',
  Void: 'neutral',
}

export const GOV_FEE_STATUS_TONE = {
  PaidByFirm: 'warning',
  InvoicedToClient: 'warning',
  Reimbursed: 'success',
}

export const PAYMENT_STATUS_TONE = {
  1: 'neutral',
  2: 'warning',
  3: 'success',
  4: 'danger',
  5: 'neutral',
  6: 'neutral',
}

/**
 * An ABSENT value is treated the same as an unrecognised one — 'warning'.
 *
 * A record that carries no status at all is not a healthy record, and giving it a
 * neutral tone would render an unknown state in the calmest colour on the page.
 * There is deliberately no separate branch for null: "missing" and "new value the
 * map has not seen" are the same class of problem and get the same treatment.
 */
function toneFor(map, value) {
  return (value == null ? undefined : map[value]) ?? 'warning'
}

/** Retainer / Service Fee status text, delegated to the shared enumLabels helper. */
export function retainerStatusText(status) {
  return retainerInvoiceStatusLabel(status) ?? null
}

export function serviceFeeStatusText(status) {
  return serviceFeeInvoiceStatusLabel(status) ?? null
}

/**
 * Government Fee status text. enumLabels.js ships the GOV_FEE_DISBURSEMENT_STATUS
 * map but no `govFeeDisbursementStatusLabel` convenience wrapper, and that file is
 * completed and protected, so the shared `enumLabel` helper is called directly
 * rather than a local label function being reinvented.
 */
export function govFeeStatusText(status) {
  return enumLabel(GOV_FEE_DISBURSEMENT_STATUS, status) ?? null
}

/** Payment Order status text — the map is NUMERIC, so no coercion is applied. */
export function paymentOrderStatusText(status) {
  return paymentStatusLabel(status) ?? null
}

/** Payment Order invoice type text — also numeric, same rule. */
export function paymentInvoiceTypeText(invoiceType) {
  return paymentInvoiceTypeLabel(invoiceType) ?? null
}

export function retainerStatusTone(status) {
  return toneFor(INVOICE_STATUS_TONE, status)
}

export function serviceFeeStatusTone(status) {
  return toneFor(INVOICE_STATUS_TONE, status)
}

export function govFeeStatusTone(status) {
  return toneFor(GOV_FEE_STATUS_TONE, status)
}

export function paymentOrderStatusTone(status) {
  return toneFor(PAYMENT_STATUS_TONE, status)
}

/**
 * Shorthand for "an amount plus the currency it is actually stored in".
 *
 * WHY THE RAW CURRENCY CODE IS RETURNED ALONGSIDE THE FORMATTED AMOUNT.
 * `formatMoney` is the canonical, currency-aware formatter and is used for the
 * figure itself. It is not sufficient on its own: the module's currency column
 * is free text with no validation and no lookup table anywhere in the platform, so
 * a stored code may be a currency Intl cannot format, and `formatMoney` then falls
 * back to `"<code> <amount>"`. Rendering only the formatted string would make a
 * currency mismatch look like a formatted success. Returning the raw code lets
 * every call site show BOTH, so the stored value stays visible and auditable
 * whether or not formatting succeeded.
 *
 * It must NOT be assumed to be AED. `clientDisplay.formatCurrency` is hardcoded
 * to AED and is wrong for this module; `formatMoney` is the only formatter used.
 */
export function moneyParts(amount, currency) {
  const rawCurrency = presentText(currency)

  return {
    formatted: formatMoney(amount, rawCurrency),
    rawCurrency,
    hasCurrency: Boolean(rawCurrency),
  }
}

/**
 * A date, or the honest missing mark. `formatDate` returns null for an absent or
 * unparseable value, which a detail row would render as a dash for us but a table
 * cell would render as nothing, so table call sites use this instead.
 */
export function dateText(value) {
  return formatDate(value) ?? MISSING_VALUE
}

export function dateTimeText(value) {
  return formatDateTime(value) ?? MISSING_VALUE
}

/**
 * Shortens a Guid to an explicitly UNRESOLVED reference.
 *
 * Bare Guids reach this module from relationships the Admin API cannot resolve
 * from a list: a retainer invoice's service contract, a service-fee invoice's
 * renewal task, a gov-fee disbursement's optional task, and a payment order's
 * linked invoice. None of those has a destination this page may link to, so they
 * are rendered as reference text and never as a name. The full value stays
 * available in the element's `title`.
 */
export function shortGuid(value) {
  const text = presentText(value)
  if (!text) return null
  return text.length > 8 ? `${text.slice(0, 8)}…` : text
}

/**
 * The four relationship descriptors, each returning ONLY what its own DTO
 * carries. None of them resolves a name, and none of them links anywhere: there is
 * no Admin contracts list route, and /renewal-tasks accepts no task-id parameter,
 * so a link here would be a dead control.
 */

/** Retainer invoice -> its service contract. `contractNumber` is DETAIL-only. */
export function contractReference(invoice) {
  const id = invoice?.serviceContractId ?? null
  const number = presentText(invoice?.contractNumber)

  return {
    present: Boolean(id),
    id,
    number,
    label: number ?? shortGuid(id),
    fullLabel: number ? `${number} · ${shortGuid(id)}` : (shortGuid(id) ?? null),
  }
}

/** Service Fee invoice -> its renewal task. ID-ONLY on both list and detail. */
export function renewalTaskReference(invoice) {
  const id = invoice?.renewalTaskId ?? null
  return { present: Boolean(id), id, label: shortGuid(id) }
}

/**
 * Government Fee disbursement -> its OPTIONAL renewal task. The column is a
 * nullable Guid on the DTO, so `present` is false for a fee that is not tied to
 * one; that is an absence and must be shown as such, not as a dangling id.
 */
export function govFeeTaskReference(disbursement) {
  const id = disbursement?.renewalTaskId ?? null
  return { present: Boolean(id), id, label: shortGuid(id) }
}

/**
 * Payment order -> the invoice it pays.
 *
 * `invoiceType` is the authoritative statement of WHICH of the two nullable id
 * columns is populated, so it is read FIRST and decides which column is allowed to
 * be shown. The switch is on the raw numeric value (1 = RetainerInvoice,
 * 2 = ServiceFeeInvoice, per PaymentInvoiceType) and never on a coerced string, so
 * an unrecognised ordinal falls to the default arm instead of being guessed at.
 *
 * Two cases the naive `retainerId ?? serviceFeeId` gets wrong, and why the switch
 * is here instead:
 *   - BOTH ids populated. The backend populates exactly one, so a record with two is
 *     already inconsistent; showing the retainer one would pick a winner silently.
 *   - Type and populated column DISAGREE. Reading both columns and trusting the
 *     first non-null would report a retainer link for a Service Fee payment. Trusting
 *     the type instead makes the disagreement visible, because the type's column is
 *     empty and nothing is claimed.
 */
export function linkedInvoiceReference(payment) {
  const typeLabel = paymentInvoiceTypeText(payment?.invoiceType)
  const retainerId = payment?.retainerInvoiceId ?? null
  const serviceFeeId = payment?.serviceFeeInvoiceId ?? null

  let id = null
  switch (payment?.invoiceType) {
    case 1:
      id = retainerId
      break
    case 2:
      id = serviceFeeId
      break
    default:
      id = null
  }

  return {
    present: Boolean(id),
    id,
    typeLabel,
    label: typeLabel && shortGuid(id) ? `${typeLabel} · ${shortGuid(id)}` : (shortGuid(id) ?? null),
  }
}

/**
 * The gateway's own order id. It is NOT a Guid — it is a free-text string issued
 * by Razorpay — so `shortGuid` would truncate it misleadingly. It is rendered
 * whole, in a monospace face, and is never shortened into something that still
 * looks like a complete identifier.
 */
export function gatewayOrderText(value) {
  return presentText(value)
}

/**
 * Honest copy for the states this module cannot present as ordinary data. Kept
 * here, next to the facts they describe, so the wording cannot drift away from the
 * limitation it explains.
 */

/**
 * SERVICE FEE INVOICES ARE CURRENTLY UNREACHABLE, and this is the one empty state
 * that must not read as "you filtered too hard".
 *
 * A service-fee invoice is created by ServiceFeeInvoiceCreationService when a
 * renewal task reaches the `Updated` state. Reaching `Updated` requires
 * `ServiceFeeAmount > 0`, and no reachable Admin path ever assigns that amount —
 * so in an API-driven environment the tab is legitimately empty. The wording says
 * exactly that, without claiming the endpoint is broken and without suggesting the
 * reader has misconfigured a filter.
 */
export const SERVICE_FEE_UNREACHABLE_NOTE =
  'A Service Fee invoice is produced when a renewal task reaches the Updated ' +
  'state, and the backend only allows that transition once the task has a Service ' +
  'Fee Amount above zero. No reachable workflow assigns that amount today, so this ' +
  'list is normally empty. The endpoint is working — there is simply nothing for ' +
  'it to return yet.'

export const SERVICE_FEE_EMPTY_TITLE = 'No Service Fee invoices.'

/**
 * WHY THERE IS NO PAYMENT STATUS FILTER.
 *
 * GET /api/v1/payment-orders accepts a `status` parameter syntactically, and the
 * read repository builds a `po."Status" = @Status` predicate — but the parameter
 * is a string and the column is an integer, so PostgreSQL rejects the comparison
 * with "operator does not exist: integer = text" and the request fails with a
 * 500. Offering the control would therefore hand the reader a filter that breaks
 * the page. This is a backend defect, stated as one, and it is a reason the
 * control is absent rather than a claim that filtering is unnecessary.
 */
export const PAYMENT_STATUS_FILTER_COPY =
  'No status filter is offered here. The payment-order list endpoint accepts a ' +
  'status parameter, but the backend passes it as text against an integer column, ' +
  'so the request fails with a server error. The gap is in the backend filter, ' +
  'not in this page, and the control is withheld until it is fixed.'

/** Payments exposes no per-status counts either, for the same reason. */
export const PAYMENT_STATUS_COUNT_COPY =
  'Only a single total is shown for payment orders, because per-status counts would ' +
  'have to come from the same unusable status filter.'

/**
 * MONEY IS NEVER AGGREGATED IN THIS MODULE, and this is why the copy says so.
 *
 * `currency` is an unvalidated free-text column on every billing table and Razorpay
 * is handed the value verbatim, so amounts across records are NOT known to share a
 * unit. There is no server-side aggregate endpoint. Summing them in the browser
 * would produce a number that silently adds different currencies together, and no
 * per-currency grouping endpoint exists to do it correctly. So there is no
 * outstanding total, no billed total, no collected total and no cross-tab money
 * figure anywhere in this module — only per-record amounts, each shown with the
 * currency it is actually stored in.
 */
export const NO_MONEY_AGGREGATION_NOTE =
  'Amounts are shown per record and are never added together. The currency column ' +
  'is free text and is not validated anywhere in the platform, and no endpoint ' +
  'aggregates by currency, so a total across records could silently mix units. ' +
  'For the same reason this page shows no outstanding, billed or collected figure.'

/**
 * THE WRITE POSITION, STATED RATHER THAN IMPLIED.
 *
 * This module is no longer read-only. Ten billing mutations are wrapped and wired:
 * three per invoice type (create, mark-paid, void), two for government fees (create,
 * status advance) and two for payment orders (create, cancel).
 *
 * EVERY ONE OF THEM IS ONE-WAY. There is no unvoid, no un-pay, no reopen, no
 * status rollback and no uncancel anywhere in the Admin API, so none of these
 * controls can offer an undo. That is why each one is reached through the shared
 * ConfirmDialog, and why the copy around each action states the irreversibility
 * rather than leaving it to be discovered.
 */
export const BILLING_LIFECYCLE_NOTE =
  'This record can be changed: an invoice can be marked paid or voided, a ' +
  'government fee can advance a status, and a payment order can be created or ' +
  'cancelled. Every one of those is a one-way change — the platform exposes no ' +
  'route that reverses any of them — so each is confirmed before it runs and ' +
  'none of them can be undone afterwards.'

/** Where the confirmation copies live, so the wording cannot drift from the guards. */
export const IRREVERSIBLE_ACTIONS = {
  MARK_PAID: 'mark-paid',
  VOID: 'void',
  GOV_FEE_STATUS: 'gov-fee-status',
  PAYMENT_CREATE: 'payment-create',
  PAYMENT_CANCEL: 'payment-cancel',
}

/**
 * The mark-paid confirmation has to name the second record it changes.
 *
 * MarkRetainerInvoicePaidCommandHandler and its Service Fee twin each lock the
 * invoice row AND the linked PaymentOrder row in one transaction, set the
 * invoice to Paid and set that order to Paid in the same commit. So marking an
 * invoice paid is not a single-record edit, and a confirmation that described it
 * as one would understate what the button does.
 */
export const MARK_PAID_SIDE_EFFECT_NOTE =
  'This also sets the payment order linked to this invoice to Paid, in the same ' +
  'transaction. Both records change together, and neither can be set back.'

/**
 * THE VOID CONFIRMATION HAS TO NAME THE GATEWAY CLOSE, FOR THE SAME REASON.
 *
 * VoidRetainerInvoiceCommandHandler and its Service Fee twin now lock the linked
 * PaymentOrder, close it with the gateway and set it to Cancelled, then void the
 * invoice — all in one transaction. A confirmation that described voiding as an
 * invoice-only edit would leave the operator thinking any linked gateway order is
 * untouched, and therefore still payable after the invoice they just voided.
 */
export const VOID_SIDE_EFFECT_NOTE =
  'If a payment order is linked to this invoice, it is closed with the payment ' +
  'provider and set to Cancelled in the same transaction, so it can no longer be ' +
  'paid. If the provider will not close it, nothing is voided.'

/**
 * A LINKED ORDER OF `Cancelled` MAKES MARK-PAID PERMANENTLY INAPPLICABLE.
 *
 * MarkRetainerInvoicePaidCommandHandler and its Service Fee twin now treat
 * Cancelled as terminal and refuse the write with a 409. So on such an invoice the
 * button is withheld and replaced by an explanation, rather than rendered disabled:
 * the rest of this file's convention is that a permanently inapplicable action is
 * absent, because a greyed-out button reads as "not right now" and invites a retry
 * that can only ever fail again.
 *
 * VOID IS STILL OFFERED, deliberately. A pending invoice with a cancelled order is
 * exactly the state voiding cleans up, and the void handler is what closes the
 * gateway — so withholding it too would leave the operator with no way forward.
 *
 * The status is matched on the string AND the ordinal, because `PaymentStatus` is
 * rendered by the global JsonStringEnumConverter as a name but arrives as a number
 * on any response that escapes that converter, and "Cancelled" vs 5 is the
 * difference between withholding a button and not.
 */
export const MARK_PAID_CANCELLED_ORDER_NOTE =
  'This invoice has a cancelled payment order linked to it, so it cannot be ' +
  'marked as paid. A cancelled order is closed at the payment provider and can ' +
  'never be settled. Void the invoice and raise a new payment order instead.'

/**
 * THE VOID REASON IS OPTIONAL, AND THAT IS THE SERVER\'S RULE NOT OURS.
 *
 * VoidRetainerInvoiceRequest and VoidServiceFeeInvoiceRequest are both a single
 * nullable `string? Reason`, and both handlers store `request.Reason?.Trim()`. No
 * validator on either command requires it. So the field is offered, explained as
 * optional, and an empty submission is sent as null rather than blocked here —
 * requiring it would be the frontend inventing a rule the server does not have,
 * and refusing a void the server would have accepted.
 */
export const VOID_REASON_HINT =
  'Optional. The server accepts a void with no reason and stores whatever text ' +
  'you send, so you can leave this empty. A reason is only useful as a record for ' +
  'whoever reads the audit log later.'

export const VOID_REASON_REQUIRED_BY_SERVER = false

/**
 * ZERO IS A VALID AMOUNT ON EVERY BILLING CREATE.
 *
 * Each validator is `Amount >= 0` — CreateRetainerInvoiceCommandValidator,
 * CreateServiceFeeInvoiceCommandValidator and CreateGovFeeDisbursementValidator
 * all state exactly that, with the message "Amount must be greater than or equal
 * to 0." So zero is admitted by the server and this UI does not invent a minimum
 * above it. It warns, because a zero-valued record records no money while looking
 * like a charge, and it keeps submit enabled, because blocking it would be a rule
 * the server does not have.
 */
export const ZERO_AMOUNT_WARNING =
  'This amount is zero. The server accepts it — the only amount rule it has is ' +
  'that it cannot be negative — so this will be submitted and will create a record ' +
  'that bills nothing.'

/** The server's only amount rule, quoted so the form cannot imply a stricter one. */
export const AMOUNT_RULE_NOTE =
  'The only amount rule the server applies is that it cannot be negative.'

/**
 * WHY CURRENCY IS A FREE-TEXT BOX AND NOT A PICKLIST.
 *
 * `Currency` is `string` on all three create requests and `string` on every
 * billing table, with no lookup table, no enum and no validation beyond being
 * non-empty. Razorpay is then handed the stored value verbatim. A dropdown would
 * have to invent the set of acceptable codes, so the field is a text input that
 * says so — an editor can enter a code this platform has never seen, and the
 * server will store it exactly as typed.
 */
export const CURRENCY_FIELD_NOTE =
  'Free text. The server stores this string as typed and passes it to the payment ' +
  'gateway unchanged — there is no currency list or validation anywhere in the ' +
  'platform, so use the exact code you intend.'

/**
 * THE GOVERNMENT FEE STATUS IS FORWARD-ONLY, AND THE CHAIN IS THE HANDLER'S.
 *
 * UpdateGovFeeDisbursementStatusCommandHandler switches on the CURRENT status and
 * permits exactly one target in each case:
 *
 *   PaidByFirm        -> only InvoicedToClient  (sets InvoicedAt)
 *   InvoicedToClient  -> only Reimbursed        (sets ReimbursedAt)
 *   Reimbursed        -> 409, "already been reimbursed. No further transitions"
 *   anything else     -> 409
 *
 * So there is exactly one legal next status from each non-terminal state, and no
 * target is chosen by the reader. `nextGovFeeStatus` derives that single target
 * from the map below rather than from arithmetic on the ordinals, so the UI cannot
 * offer a jump the handler would reject, and `Reimbursed` — which has no successor
 * — returns null and therefore renders no control at all.
 */
export const GOV_FEE_STATUS_NEXT = {
  PaidByFirm: 'InvoicedToClient',
  InvoicedToClient: 'Reimbursed',
}

/**
 * The one legal successor for `status`, or null when the record is terminal or the
 * backend has added a status this map has not seen. An unrecognised status returns
 * null rather than a guess, so a new enum member shows as "no further transition"
 * instead of being offered a transition that would 409.
 */
export function nextGovFeeStatus(status) {
  return status == null ? null : (GOV_FEE_STATUS_NEXT[status] ?? null)
}

/** What each advance means, stated in the record's own terms rather than the enum's. */
export const GOV_FEE_STATUS_ADVANCE_MEANING = {
  InvoicedToClient:
    'Records that the firm has invoiced the client for this fee, and stamps the ' +
    'Invoiced to Client timestamp. The firm is still out of pocket until the fee ' +
    'is reimbursed.',
  Reimbursed:
    'Records that the client has paid the fee back to the firm, and stamps the ' +
    'Reimbursed timestamp. This is the only status on which the money is home.',
}

/** Why a Reimbursed disbursement offers no status control at all. */
export const GOV_FEE_TERMINAL_NOTE =
  'This disbursement is Reimbursed, which is the end of its chain. The backend ' +
  'rejects any further status change for it, so no control is offered.'

/** The two request shapes, so the status dialog cannot ask for a field it lacks. */
export const GOV_FEE_STATUS_ADVANCE_REQUEST_FIELDS_NOTE =
  'The status endpoint takes one field — the target status — and nothing else. The ' +
  'fee description, reference, amount and currency on this record are not ' +
  'editable through it.'

/**
 * SERVICE-FEE CREATION IS GATED, NOT BUILT, AND THIS IS WHY.
 *
 * POST /service-fee-invoices exists and is Admin-authorized, but
 * ServiceFeeInvoiceCreationService refuses the request unless the renewal task is
 * in `Updated` (step 4) and — the part that matters — reaching `Updated` requires
 * `ServiceFeeAmount > 0`, which no reachable workflow assigns. So the list of
 * qualifying tasks is empty in practice and every submission would be a guaranteed
 * 409.
 *
 * The UI therefore still offers the form, because the capability is real and the
 * gate is a data condition rather than a missing feature. When no qualifying task
 * exists the submit button is disabled and this explanation is shown, so the
 * reader learns why rather than watching a request fail. Nothing fabricates a task
 * and nothing bypasses the rule.
 */
export const SERVICE_FEE_CREATE_BLOCKED_NOTE =
  'A Service Fee invoice can only be raised against a renewal task that is in the ' +
  'Updated state, and the server refuses any other task. Reaching Updated ' +
  'requires the task to carry a Service Fee Amount above zero, and no workflow in ' +
  'the platform assigns that amount today — so there is normally no qualifying ' +
  'task and nothing to submit. This is a backend prerequisite, not a fault in this ' +
  'form, and the control stays disabled until a task genuinely qualifies.'

export const SERVICE_FEE_CREATE_NEEDS_COMPANY_FIRST =
  'Choose a client company first. The qualifying tasks belong to a company, and ' +
  'the task list cannot be loaded until one is chosen.'

/**
 * The company genuinely has NO renewal tasks. Distinguishable from "none of them
 * qualify" because the unfiltered task query reports an authoritative `totalCount`
 * rather than the length of a capped page, so this is a fact and not an inference.
 */
export const SERVICE_FEE_CREATE_NO_TASKS_AT_ALL =
  'This company has no renewal tasks at all, so there is nothing to choose from. ' +
  'The task list has no search parameter, so a task can only be found from here by ' +
  'its company.'

/**
 * The company HAS tasks, and the server has confirmed none of them are `Updated`.
 * Stated separately from SERVICE_FEE_CREATE_NO_TASKS_AT_ALL so an empty picker is
 * never ambiguous: this picker lists only `Updated` tasks, fetched with the server's
 * own `?status=Updated` filter, so "empty" means none qualify rather than "the page
 * ran out".
 */
export const SERVICE_FEE_CREATE_NO_UPDATED_TASKS =
  'None of this company’s renewal tasks are in the Updated state, so there is no ' +
  'qualifying task to choose. This list shows only Updated tasks; the task filter in ' +
  'the toolbar above still lists every status.'

/**
 * The two additional server rules a Service Fee create can hit that the form
 * cannot pre-check, because neither is knowable from the task option list: the
 * invoice number must be unique across non-deleted invoices, and a task may have
 * at most one non-deleted invoice against it. Both arrive as 409 and are surfaced
 * as the server's own message rather than prevented client-side.
 */
export const SERVICE_FEE_CREATE_SERVER_ONLY_RULES =
  'Two further rules can only be checked by the server: the invoice number must ' +
  'not already be used, and the chosen task must not already have an invoice.'

/**
 * A RETAINER INVOICE IS RAISED AGAINST EXACTLY ONE CONTRACT: the company's ACTIVE
 * one, resolved server-side.
 *
 * CreateRetainerInvoiceCommandHandler requires the contract to exist, to belong
 * to the posted company (409 otherwise) and to be Active (409 otherwise). There is
 * no contract selector in the request that lets an admin pick a different one and
 * succeed, so the form resolves the company's single active contract through
 * GET /clients/{id}/contracts/active and shows it read-only rather than offering a
 * dropdown of every contract it might reject.
 *
 * The active-contract endpoint filters on Status only and never compares dates,
 * so a contract past its EndDate still comes back. The form says so rather than
 * implying the contract is current.
 */
export const RETAINER_ACTIVE_CONTRACT_CAVEAT =
  'This is the company\'s active contract, chosen by the server. The lookup matches ' +
  'on status alone and ignores dates, so a contract whose end date has already ' +
  'passed is still reported as active.'

export const RETAINER_NO_ACTIVE_CONTRACT_NOTE =
  'This company has no active service contract, so an invoice cannot be raised ' +
  'for it. The server requires the contract to be active and refuses the request ' +
  'with a conflict otherwise, so the form stays disabled rather than sending a ' +
  'request that is certain to fail. Contracts are managed on the client\'s record.'

/** Why the resolved contract is not selectable. */
export const RETAINER_CONTRACT_IS_RESOLVED_NOTE =
  'Resolved from the company\'s active contract. It cannot be changed here: the ' +
  'server accepts only that contract, so choosing another one could not succeed.'

/**
 * THE PAYMENT MODEL, AS THE BACKEND ACTUALLY IMPLEMENTS IT.
 *
 * CreatePaymentOrderCommandHandler copies Amount and Currency OFF the invoice and
 * onto the order, then calls the gateway. It never touches the invoice: the order
 * is created in `Created`, moved to `Pending` once Razorpay returns an order id,
 * and the invoice stays Pending. Only the webhook (CompletePaymentFromWebhook) or
 * an explicit invoice mark-paid settles it.
 *
 * So the admin raises the order and the CLIENT pays. Nothing in this portal takes
 * the money, and creating an order is not a receipt.
 */
export const PAYMENT_MODEL_NOTE =
  'Creating a payment order raises it with the payment gateway and records the ' +
  'client company, amount and currency from the invoice — none of which are sent ' +
  'from this form, because the server copies them itself. The client then pays ' +
  'through Razorpay. Creating an order does NOT mark the invoice paid: the ' +
  'invoice stays Pending until the gateway confirms the payment through a webhook, ' +
  'or an administrator marks the invoice paid explicitly.'

export const PAYMENT_NO_CAPTURE_NOTE =
  'There is no capture or refund control in this portal. Settling an order is the ' +
  'gateway webhook\'s job, and the server exposes no route to capture, refund or ' +
  'reverse a payment.'

/** The request body is exactly four fields, and the two id columns are exclusive. */
export const PAYMENT_ONE_INVOICE_ONLY_NOTE =
  'A payment order references exactly one invoice. The request carries the type ' +
  'and the matching id, and the server rejects a request that names the other ' +
  'kind, so only the id column for the chosen type is ever sent.'

/** Only pending invoices can be paid, so only pending invoices are offered. */
export const PAYMENT_PENDING_ONLY_NOTE =
  'Only pending invoices can be paid — the server refuses a payment against an ' +
  'invoice that is already paid or voided — so this list is every pending invoice ' +
  'of the chosen company.'

/** Invoice numbers exist on BOTH invoice types, so no fallback label is invented. */
export const PAYMENT_INVOICE_LABEL_NOTE =
  'Each option shows the invoice number with its amount, because both invoice ' +
  'types carry an invoice number on both the list and the detail.'

/**
 * CANCELLING A PAYMENT ORDER CLOSES IT AT THE GATEWAY FIRST.
 *
 * CancelPaymentOrderCommandHandler now locks the order, closes the Razorpay order
 * through the gateway, and only then sets Status = Cancelled, stamps CancelledAt
 * and writes the audit row — all in one transaction. So a successful cancel means
 * the money genuinely can no longer be taken on this order, and the copy may say
 * so. It still does NOT refund anything and does NOT touch the linked invoice: an
 * invoice that was Pending stays Pending, and is settled by a new payment order.
 *
 * The duplicate guard in CreatePaymentOrderCommandHandler tests whether an order
 * for the invoice already carries a RazorpayOrderId, NOT the order's status. Since
 * cancellation keeps that id, a cancelled order permanently occupies the slot and a
 * fresh order cannot be raised for that invoice through this path.
 */
export const PAYMENT_CANCEL_NOTE =
  'This closes the order with the payment provider and sets it to Cancelled ' +
  'here. It does not refund anything, and it does not change the linked ' +
  'invoice — an invoice that was pending stays pending.'

export const PAYMENT_CANCEL_CONSEQUENCE_NOTE =
  'It cannot be undone, and it blocks this invoice from getting a new payment ' +
  'order: the server refuses a new order when one already exists for the invoice ' +
  'with a gateway reference, and cancelling keeps that reference.'

export const PAYMENT_CANCEL_ELIGIBILITY_NOTE =
  'Only an order in Created or Pending can be cancelled. The server rejects the ' +
  'request for any other status.'

/**
 * THE PROVIDER CAN REFUSE, AND IF IT DOES NOTHING HAPPENS AT ALL.
 *
 * This replaces the old late-payment warning. That warning existed because cancel
 * used to leave the gateway order open, so a client could still settle it against a
 * record reading Cancelled. The gateway order is now closed, so that race is gone —
 * and the residual risk is the opposite one: a close that fails leaves the order
 * open, which is why the failure has to be loud rather than silent.
 */
export const PAYMENT_CANCEL_GATEWAY_NOTE =
  'If the provider will not close the order, the whole cancellation is refused ' +
  'and nothing changes here — the order stays open and payable.'

/**
 * Field length ceilings, read off the server\'s validators so the forms do not
 * guess them. Each is a MaximumLength rule on a specific request field.
 */
export const BILLING_FIELD_LIMITS = {
  invoiceNumber: 100,
  feeDescription: 2000,
  paymentNotes: 5000,
}

/**
 * The invoice numbers the server insists on being unique, per invoice type, so a
 * duplicate is described as a duplicate rather than as a generic failure.
 * CreateRetainerInvoiceCommandHandler and ServiceFeeInvoiceCreationService both
 * test InvoiceNumber across non-deleted rows of their own table and raise 409.
 */
export const INVOICE_NUMBER_UNIQUE_NOTE =
  'Must not already be used by another invoice of this type — the server rejects ' +
  'a duplicate with a conflict.'

/**
 * Confirmation copy, keyed by action, so the irreversible wording is written once
 * and every surface that offers the action says the same thing. The sentences that
 * name a specific record (the invoice number, the current status) are assembled at
 * the call site; these are the parts that do not vary.
 *
 * `tone` follows ConfirmDialog's own three values: 'default', 'warning', 'danger'.
 * Money that disappears is 'danger'; a status that moves forward is 'warning',
 * because it cannot be walked back either but nothing is lost.
 */
export const BILLING_ACTION_CONFIRMS = {
  [IRREVERSIBLE_ACTIONS.MARK_PAID]: {
    title: 'Mark this invoice as paid?',
    tone: 'danger',
    confirmLabel: 'Mark as paid',
    irreversible:
      'The status becomes Paid and a paid timestamp is recorded. There is no route ' +
      'that sets an invoice back to Pending, so this cannot be undone.',
  },
  [IRREVERSIBLE_ACTIONS.VOID]: {
    title: 'Void this invoice?',
    tone: 'danger',
    confirmLabel: 'Void invoice',
    irreversible:
      'The status becomes Void and a void timestamp is recorded. Void is terminal: ' +
      'a voided invoice can never be paid and can never be reopened.',
  },
  [IRREVERSIBLE_ACTIONS.GOV_FEE_STATUS]: {
    title: 'Advance this disbursement?',
    tone: 'warning',
    confirmLabel: 'Advance status',
    irreversible:
      'The status moves one step forward and a timestamp is recorded for that ' +
      'step. The chain has no reverse, so this cannot be undone.',
  },
  [IRREVERSIBLE_ACTIONS.PAYMENT_CREATE]: {
    title: 'Raise a payment order?',
    tone: 'warning',
    confirmLabel: 'Raise payment order',
    irreversible:
      'The order is created with the payment gateway, and the server refuses a ' +
      'second order for an invoice that already has a gateway reference.',
  },
  [IRREVERSIBLE_ACTIONS.PAYMENT_CANCEL]: {
    title: 'Cancel this payment order?',
    tone: 'danger',
    confirmLabel: 'Cancel payment order',
    irreversible:
      'There is no route that reopens a cancelled order, and cancelling keeps the ' +
      'gateway reference, so this invoice can never get a new payment order.',
  },
}

/**
 * Button labels for the actions, matching the confirm labels they open. The four
 * CREATE labels live here too, for the same reason: the page builds one button per
 * tab from a single map, so the label that opens a dialog and the copy inside it
 * are edited in one file.
 */
export const BILLING_ACTION_LABELS = {
  [IRREVERSIBLE_ACTIONS.MARK_PAID]: 'Mark as paid',
  [IRREVERSIBLE_ACTIONS.VOID]: 'Void',
  [IRREVERSIBLE_ACTIONS.GOV_FEE_STATUS]: 'Advance status',
  [IRREVERSIBLE_ACTIONS.PAYMENT_CREATE]: 'Raise payment order',
  [IRREVERSIBLE_ACTIONS.PAYMENT_CANCEL]: 'Cancel order',
}

/**
 * The create button for each tab, keyed by the same `BILLING_TABS` keys the page
 * already switches on, so a tab cannot be added without a label for it.
 *
 * THE VERB IS DELIBERATE ON EACH ONE. "Create" would be wrong for the retainer and
 * service-fee invoices, which are not resources the client ever sees but amounts the
 * firm is claiming, and it would be actively misleading for a payment order, which
 * creates a REAL Razorpay order and an obligation to pay. The labels name the thing
 * being made, not a database concept.
 */
export const BILLING_CREATE_LABELS = {
  retainer: 'Raise retainer invoice',
  'service-fee': 'Raise Service Fee invoice',
  'gov-fee': 'Record fee paid',
  payments: BILLING_ACTION_LABELS[IRREVERSIBLE_ACTIONS.PAYMENT_CREATE],
}

/**
 * The header strapline for the whole page.
 *
 * THE OLD ONE ENDED "…as tabs of a single page. Read-only." and the final sentence
 * is removed because it is now false in all four tabs: two of them create records,
 * and every one of them has a way to change a record's state. What replaces it says
 * the same thing about structure and adds the one fact a reader needs before
 * choosing a tab — that raising a record is per-record work done by an
 * administrator, not an automatic consequence of a contract or a task.
 */
export const BILLING_PAGE_SUBTITLE =
  'Retainer, Service Fee and Government Fee billing, plus payment orders, as tabs of a single page. Each tab raises and changes its own records.'

/**
 * Per-tab list straplines, replacing the single "Newest first. Read-only." that every
 * card shared. The sort order is unchanged; the trailing claim is not, so each line
 * now describes what the tab can actually be used for.
 */
export const BILLING_LIST_SUBTITLES = {
  retainer: 'Newest first. An invoice is raised against a client\u2019s active service contract, and can later be marked paid or voided.',
  'service-fee':
    'Newest first. An invoice is raised against a renewal task, and can later be marked paid or voided.',
  'gov-fee':
    'Newest first. A record here is a fee the firm has already paid, not an invoice, and its status only moves forward.',
  payments:
    'Newest first. An order here is a live gateway order, and the client pays it \u2014 not this portal.',
}

/**
 * The invoice number limit is enforced on the client AND backed by the server's own
 * MaximumLength rule, so the message can name the real ceiling rather than a
 * round number the reader would have to discover by being rejected.
 */
export const INVOICE_NUMBER_LIMIT_NOTE = `Must be 1 to ${BILLING_FIELD_LIMITS.invoiceNumber} characters — the server rejects a longer value.`

/** The four entity types the audit log is filtered by, verified against the handlers. */
export const BILLING_AUDIT_ENTITY_TYPES = {
  retainer: 'RetainerInvoice',
  'service-fee': 'ServiceFeeInvoice',
  'gov-fee': 'GovFeeDisbursement',
  payment: 'PaymentOrder',
}

/**
 * The four tabs, in the order they appear, with the heading and strapline each one
 * shows above its list.
 *
 * THE METADATA LIVES HERE RATHER THAN IN THE TABLIST COMPONENT because the page also
 * needs the title and subtitle, to head the tab panel. Exporting that table from the
 * component file would make the module export something other than a component, which
 * breaks React Fast Refresh for every editor working on the page; keeping it in the
 * presentation vocabulary module lets the tablist export only its component and lets
 * the page read the same strings from one place, so the tab label and the panel
 * heading cannot drift apart.
 *
 * They are TABS, not pages, and the difference is load-bearing: ONE shared company
 * filter sits above this list, so switching tabs must not reset it, and each tab
 * keeps its own page and status. There is no deep link to any of them, because no
 * Admin route accepts a tab parameter and inventing one would be a control nothing
 * reads.
 */
export const BILLING_TABS = [
  {
    key: 'retainer',
    label: 'Retainer',
    icon: Receipt,
    title: 'Retainer invoices',
    subtitle:
      'Monthly invoices raised against a service contract. Newest first. Invoices can be created, marked paid or voided.',
  },
  {
    key: 'service-fee',
    label: 'Service Fee',
    icon: FileText,
    title: 'Service Fee invoices',
    subtitle:
      'One-off invoices raised when a renewal task is completed. Newest first. Invoices can be marked paid or voided; creating one is gated on a task prerequisite the backend does not currently satisfy.',
  },
  {
    key: 'gov-fee',
    label: 'Government Fees',
    icon: Landmark,
    title: 'Government fee disbursements',
    subtitle:
      'Government fees the firm has paid and is waiting to be reimbursed. Newest first. A disbursement can be created and its status advanced one step at a time.',
  },
  {
    key: 'payments',
    label: 'Payments',
    icon: CreditCard,
    title: 'Payment orders',
    subtitle:
      'Gateway payment orders raised against an invoice. Newest first. Orders can be created and cancelled; the client pays through the gateway.',
  },
]

/** The label maps, re-exported so call sites need one import for this module. */
export const STATUS_LABELS = {
  retainer: RETAINER_INVOICE_STATUS,
  serviceFee: SERVICE_FEE_INVOICE_STATUS,
  govFee: GOV_FEE_DISBURSEMENT_STATUS,
  payment: PAYMENT_STATUS,
  paymentInvoiceType: PAYMENT_INVOICE_TYPES,
}
