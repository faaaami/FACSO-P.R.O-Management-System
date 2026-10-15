import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  createAdminGovFeeDisbursement,
  createAdminRetainerInvoice,
  createAdminServiceFeeInvoice,
  markAdminRetainerInvoicePaid,
  markAdminServiceFeeInvoicePaid,
  updateAdminGovFeeDisbursementStatus,
  voidAdminRetainerInvoice,
  voidAdminServiceFeeInvoice,
} from '../../api/admin/invoices'
import {
  cancelAdminPaymentOrder,
  createAdminPaymentOrder,
} from '../../api/admin/payments'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin billing write operations — all ten of them.
 *
 * Ten mutations, three resources, one file, and they are ALL ONE-WAY. There is no
 * unvoid, no un-pay, no status rollback, no uncancel and no reopen anywhere in the
 * Admin API, so not one of these can offer an undo. Every caller must therefore put
 * its action behind the shared ConfirmDialog first, and the confirmation copy for
 * each one is centralised in components/admin/invoices/billingDisplay.js
 * (BILLING_ACTION_CONFIRMS) so the wording cannot drift between surfaces.
 *
 * The ten are:
 *
 *   Retainer invoice      create, mark-paid, void
 *   Service Fee invoice   create, mark-paid, void
 *   Government Fee        create, status advance
 *   Payment Order         create, cancel
 *
 * ------------------------------------------------------------------
 * NO OPTIMISTIC UPDATES, EVER, ON ANY OF THEM.
 * ------------------------------------------------------------------
 * Every one of these writes money or a terminal status, and none can be undone, so
 * there is no such thing as a safe provisional value to show: a rejected optimistic
 * write would have to be rolled back visually, which is precisely the illusion a
 * one-way change cannot afford. Each mutation waits for the server, then
 * invalidates. The invalidations are the only thing that makes the UI move.
 *
 * ------------------------------------------------------------------
 * WHY SUCCESS AND ERROR TOASTS LIVE HERE AND NOT IN THE DIALOGS.
 * ------------------------------------------------------------------
 * So that all ten are guaranteed to report their outcome exactly once. A dialog
 * that forgot its toast would otherwise fail silently, and two surfaces toasting
 * the same failure would double it. The dialogs keep the parts only they can do:
 * closing themselves, mapping server field errors onto inputs with `apiFieldErrors`,
 * and showing the inline error banner. `extractApiErrorMessage` is used here rather
 * than a fixed string because the server's own message is the specific one — a 409
 * names the exact rule that was broken (contract not active, invoice number taken,
 * "only pending invoices can be paid") and replacing it with generic text would
 * throw away the only information the operator needs.
 */

/* -------------------------------------------------------------------------- */
/*  CACHE KEYS — read out of the query hooks, not guessed                       */
/* -------------------------------------------------------------------------- */

/**
 * EVERY MUTATION INVALIDATES THESE TWO, without exception.
 *
 * `['admin','audit-log']` — every one of the ten handlers writes an audit row
 * (RetainerInvoiceCreated / …Paid / …Voided, ServiceFeeInvoiceCreated / …Paid /
 * …Voided, GovFeeDisbursementCreated / …StatusUpdated, PaymentOrderCreated /
 * PaymentOrderRetried / PaymentOrderCancelled). The per-record Activity tab reads
 * the audit log through AdminBillingActivitySection, so without this an
 * administrator would perform the action, open the tab that exists to show it, and
 * see a trail that does not contain it — the cache's 30s staleTime means it would
 * not self-correct either.
 *
 * `['admin','dashboard']` — the pending-invoice KPIs and the dashboard summary do
 * NOT live under `['admin','invoices']`, so a billing status change would never
 * reach them. `useAdminDashboard.js` caches them as
 * `['admin','dashboard','pending-invoices', { kind }]` and
 * `['admin','dashboard','summary']`; a mark-paid that skipped this would leave the
 * dashboard claiming a pending invoice is still pending. The prefix is used rather
 * than the two specific keys because the summary's billing figures are not
 * documented as a fixed set, and a prefix cannot be wrong about a key this file has
 * not read. It is also close to free: TanStack refetches only queries that are
 * mounted and enabled, and while the reader is on the Invoices page none of the
 * dashboard queries are mounted at all.
 */
const ALWAYS_INVALIDATED = [
  ['admin', 'audit-log'],
  ['admin', 'dashboard'],
]

/** The three invoice list prefixes, one per resource. */
const RETAINER_LIST_KEY = ['admin', 'invoices', 'retainer']
const SERVICE_FEE_LIST_KEY = ['admin', 'invoices', 'service-fee']
const GOV_FEE_LIST_KEY = ['admin', 'invoices', 'gov-fee']

/**
 * THE PAYMENT LIST AND THE PAYMENT DETAIL ARE DIFFERENT KEYS.
 *
 * `useAdminPayments.js` uses `['admin','payments', { … }]` for the list and
 * `['admin','payment', id]` for one order. 'payment' and 'payments' are different
 * strings, so neither prefix matches the other and both have to be named.
 */
const PAYMENT_LIST_KEY = ['admin', 'payments']
const PAYMENT_DETAIL_KEY = ['admin', 'payment']

/**
 * THE SINGULAR DETAIL KEYS DO NOT FALL UNDER THE PLURAL LIST PREFIX.
 *
 * `['admin','invoices','retainer', { … }]` (list) and `['admin','invoice','retainer',
 * id]` (detail) share no prefix: 'invoices' is not a prefix of 'invoice' in an
 * array key, because matching is element-wise and the third elements differ. An
 * open drawer keeps its detail under the singular key, so a mutation that
 * invalidated only the list would leave the drawer showing the record's PRE-mutation
 * status next to a list that already says otherwise. Both are always invalidated
 * for a single-record mutation, which is the whole reason this file is careful about
 * the difference.
 */
function invoiceDetailKey(kind, id) {
  return ['admin', 'invoice', kind, id]
}

/** `kind` is this module's vocabulary: 'retainer' | 'service-fee' | 'gov-fee'. */
function invoiceListKey(kind) {
  return ['admin', 'invoices', kind]
}

/**
 * THE BACKEND USES TWO WIRE FORMS OF `InvoiceType`, AND BOTH LAND ON THIS FILE.
 *
 *   - as a NUMBER (1 | 2): every READ DTO declares `int InvoiceType` —
 *     GetPaymentOrderByIdResponseDto among them — so the list, the detail, and
 *     whatever an already-open drawer is holding are all numeric.
 *   - as a NAME ("RetainerInvoice" | "ServiceFeeInvoice"): the command RESPONSES
 *     declare the enum, and Program.cs's global JsonStringEnumConverter renders it
 *     as a string. Both CreatePaymentOrderResponseDto and
 *     CancelPaymentOrderResponseDto are shaped this way.
 *
 * So this helper accepts both, and it has to: a numeric-only version would resolve
 * every cancelled Service Fee order to the RETAINER list, and a string-only version
 * would resolve every newly created one there too. Both failures are silent — a
 * perfectly valid invalidate against the wrong list — which is the worst kind.
 *
 * Unrecognised input falls back to 'retainer', which is the enum's first member and
 * the safer side of the two for a cache refresh: it refreshes a real list rather
 * than skipping a refresh altogether.
 */
function invoiceKindForPaymentType(invoiceType) {
  if (invoiceType === 2 || invoiceType === 'ServiceFeeInvoice') return 'service-fee'
  return 'retainer'
}

/** Invalidates a set of keys; every mutation funnels through this one place. */
function invalidateAll(queryClient, queryKeys) {
  for (const queryKey of queryKeys) {
    queryClient.invalidateQueries({ queryKey })
  }
}

/* -------------------------------------------------------------------------- */
/*  RETAINER INVOICE                                                           */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/retainer-invoices
 *
 * `payload.serviceContractId` must be the company's ACTIVE contract, resolved by
 * the caller through `useAdminActiveContract`: the handler rejects a contract that
 * is not Active, and rejects one belonging to another company, both with 409.
 *
 * A new invoice appears in the list only, so only the list is invalidated. There is
 * no detail key for a record nothing is holding open, and seeding the cache from
 * the response would create a second copy that could disagree with the list.
 */
export function useCreateAdminRetainerInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminRetainerInvoice(payload),
    onSuccess: (created) => {
      invalidateAll(queryClient, [...ALWAYS_INVALIDATED, RETAINER_LIST_KEY])
      toast.success(
        `Retainer invoice ${created?.invoiceNumber ?? ''} created. It is pending until it is marked paid.`.trim(),
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not create the retainer invoice.'),
      )
    },
  })
}

/**
 * PATCH /api/v1/retainer-invoices/{id}/mark-paid — no body.
 *
 * INVALIDATES THE PAYMENT SIDE, because this is not a single-record change:
 * MarkRetainerInvoicePaidCommandHandler locks the linked PaymentOrder row, sets it
 * to Paid and stamps its PaidAt in the same transaction as the invoice. Leaving
 * the payment list stale would show a Pending order for an invoice this page has
 * just declared settled.
 *
 * A CANCELLED ORDER IS REFUSED (409). The handler treats Cancelled as terminal, so
 * an operator cannot resurrect a closed gateway order into a settled invoice. The
 * server's own message is surfaced via extractApiErrorMessage.
 */
export function useMarkAdminRetainerInvoicePaid() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (invoiceId) => markAdminRetainerInvoicePaid(invoiceId),
    onSuccess: (invoice) => {
      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        RETAINER_LIST_KEY,
        invoiceDetailKey('retainer', invoice?.id),
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
      ])
      toast.success(
        `Invoice ${invoice?.invoiceNumber ?? ''} marked as paid. Any payment order linked to it was marked paid in the same transaction.`.trim(),
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not mark this invoice as paid.'),
      )
    },
  })
}

/**
 * PATCH /api/v1/retainer-invoices/{id}/void
 *
 * `reason` is OPTIONAL and is forwarded as given — an empty string is sent as
 * `null` by the API wrapper, which is exactly what the server stores for "no
 * reason". It is never required here, because no server validator requires it.
 *
 * THE PAYMENT KEYS ARE NOW INVALIDATED, because voiding is no longer invoice-only.
 * The handler locks the linked PaymentOrder, closes it with the gateway, sets it
 * to Cancelled and voids the invoice — all in one transaction, so a failed gateway
 * close voids nothing. Leaving the payment side stale would show a Pending order,
 * and a still-payable gateway order, for an invoice this page has just voided.
 */
export function useVoidAdminRetainerInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ invoiceId, reason }) => voidAdminRetainerInvoice(invoiceId, reason),
    onSuccess: (invoice) => {
      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        RETAINER_LIST_KEY,
        invoiceDetailKey('retainer', invoice?.id),
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
      ])
      toast.success(`Invoice ${invoice?.invoiceNumber ?? ''} voided.`.trim())
    },
    onError: (error) => {
      toast.error(extractApiErrorMessage(error, 'Could not void this invoice.'))
    },
  })
}

/* -------------------------------------------------------------------------- */
/*  SERVICE FEE INVOICE                                                        */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/service-fee-invoices
 *
 * GATED AT THE CALLER, NOT HERE. ServiceFeeInvoiceCreationService refuses the
 * request unless the renewal task is `Updated`, and it also refuses if that task
 * already has a non-deleted invoice. Reaching `Updated` requires a Service Fee
 * Amount above zero, and that amount IS reachable now — PATCH
 * /api/v1/admin/tasks/{taskId}/service-fee assigns it. An earlier version of this
 * comment claimed no reachable workflow assigned it and that the request was
 * guaranteed to fail; that is no longer accurate.
 *
 * The remaining obstacle is different: UpdateRenewalTaskStatusCommandHandler
 * publishes RenewalTaskCompletedEvent inside the status transaction and
 * CreateServiceFeeInvoiceOnTaskCompletedHandler creates the invoice in that same
 * transaction. So a task that reached `Updated` through the normal completion flow
 * already has its invoice, and this endpoint returns the server's "A service fee
 * invoice already exists for this renewal task." 409 for it. Whether this Admin
 * entry point should exist at all is an open product decision (F-33) and is NOT
 * resolved here.
 *
 * This hook stays a plain wrapper: it is not made deliberately fail, and it is not
 * hardcoded to fail, so if a qualifying task does exist the submission works
 * normally. The create dialog checks for a qualifying task and disables submission
 * when there is none, so the reader learns why instead of watching a request fail.
 *
 * One server rule CANNOT be pre-checked from the task option list and is left to
 * the server: a task may have at most one non-deleted invoice against it. That
 * arrives as a 409 and is reported as the server's own message.
 */
export function useCreateAdminServiceFeeInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminServiceFeeInvoice(payload),
    onSuccess: (created) => {
      invalidateAll(queryClient, [...ALWAYS_INVALIDATED, SERVICE_FEE_LIST_KEY])
      toast.success(
        `Service Fee invoice ${created?.invoiceNumber ?? ''} created. It is pending until it is marked paid.`.trim(),
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not create the Service Fee invoice.'),
      )
    },
  })
}

/**
 * PATCH /api/v1/service-fee-invoices/{id}/mark-paid — no body.
 *
 * As with the retainer, the handler also sets the linked PaymentOrder to Paid in
 * the same transaction, and a CANCELLED order is refused (409) as terminal.
 */
export function useMarkAdminServiceFeeInvoicePaid() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (invoiceId) => markAdminServiceFeeInvoicePaid(invoiceId),
    onSuccess: (invoice) => {
      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        SERVICE_FEE_LIST_KEY,
        invoiceDetailKey('service-fee', invoice?.id),
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
      ])
      toast.success(
        `Invoice ${invoice?.invoiceNumber ?? ''} marked as paid. Any payment order linked to it was marked paid in the same transaction.`.trim(),
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not mark this invoice as paid.'),
      )
    },
  })
}

/**
 * PATCH /api/v1/service-fee-invoices/{id}/void — optional reason. Invalidates the
 * payment keys too: like the retainer void, this one closes and cancels the linked
 * PaymentOrder in the same transaction.
 */
export function useVoidAdminServiceFeeInvoice() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ invoiceId, reason }) =>
      voidAdminServiceFeeInvoice(invoiceId, reason),
    onSuccess: (invoice) => {
      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        SERVICE_FEE_LIST_KEY,
        invoiceDetailKey('service-fee', invoice?.id),
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
      ])
      toast.success(`Invoice ${invoice?.invoiceNumber ?? ''} voided.`.trim())
    },
    onError: (error) => {
      toast.error(extractApiErrorMessage(error, 'Could not void this invoice.'))
    },
  })
}

/* -------------------------------------------------------------------------- */
/*  GOVERNMENT FEE DISBURSEMENT                                                */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/gov-fee-disbursements
 *
 * A new disbursement starts at `PaidByFirm` — the first status in the forward-only
 * chain — and `PaidByFirmAt` is stamped by the handler, so neither is a request
 * field and neither is sent.
 */
export function useCreateAdminGovFeeDisbursement() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminGovFeeDisbursement(payload),
    onSuccess: (created) => {
      invalidateAll(queryClient, [...ALWAYS_INVALIDATED, GOV_FEE_LIST_KEY])
      toast.success(
        `Disbursement created for ${created?.amount ?? ''} ${created?.currency ?? ''}. It starts at Paid by Firm.`.trim(),
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not create the disbursement.'),
      )
    },
  })
}

/**
 * PATCH /api/v1/gov-fee-disbursements/{id}/status
 *
 * `status` is the single target status, sent as its enum name. The handler permits
 * exactly one successor per current status and rejects everything else, so the
 * caller is expected to derive the target from `nextGovFeeStatus` in
 * billingDisplay.js rather than to offer a choice. A wrong target surfaces as the
 * server's own 409, which names the transition it refused.
 */
export function useUpdateAdminGovFeeDisbursementStatus() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ disbursementId, status }) =>
      updateAdminGovFeeDisbursementStatus(disbursementId, status),
    onSuccess: (updated) => {
      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        GOV_FEE_LIST_KEY,
        invoiceDetailKey('gov-fee', updated?.id),
      ])
      toast.success(`Disbursement status advanced to ${updated?.status ?? 'the next status'}.`)
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not advance this disbursement status.'),
      )
    },
  })
}

/* -------------------------------------------------------------------------- */
/*  PAYMENT ORDERS                                                             */
/* -------------------------------------------------------------------------- */

/**
 * POST /api/v1/payment-orders
 *
 * `payload.invoiceType` is the numeric PaymentInvoiceType the wrapper sends as an
 * enum name (1 = RetainerInvoice, 2 = ServiceFeeInvoice). The invoice keys are
 * derived from the value THIS REQUEST SENT rather than from the response, because
 * the create response declares `InvoiceType` as the enum — so it arrives as a
 * STRING — while the read DTOs declare it as `int` and arrive as numbers. Reading
 * the type off the response would mean handling two wire forms of the same field;
 * reading it off the request keeps this file on the numeric vocabulary the rest of
 * the module already uses.
 *
 * A NOTE ON THE INVOICE INVALIDATIONS, since it would otherwise look like a claim
 * that this action changes the invoice: it does not. Neither handler writes the
 * invoice — CreatePaymentOrderCommandHandler only reads it, to copy Amount and
 * Currency onto the order, and CancelPaymentOrderCommandHandler never touches it at
 * all. So these two invalidations currently refresh nothing that moved. They are
 * issued anyway because the payment order is read as a fact ABOUT that invoice and
 * an operator who raises or cancels one is looking at the invoice beside it; the
 * cost is nil while the reader is on the Payments tab, where no invoice query is
 * mounted. The comment is here so nobody later mistakes the invalidation for
 * evidence that the invoice changed.
 */
export function useCreateAdminPaymentOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (payload) => createAdminPaymentOrder(payload),
    onSuccess: (created, variables) => {
      const kind = invoiceKindForPaymentType(variables?.invoiceType)
      const linkedInvoiceId = variables?.retainerInvoiceId ?? variables?.serviceFeeInvoiceId

      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
        invoiceListKey(kind),
        ...(linkedInvoiceId ? [invoiceDetailKey(kind, linkedInvoiceId)] : []),
      ])

      // The order is raised, not settled. Saying so in the toast is the difference
      // between an accurate report and one that implies the invoice is now paid.
      toast.success(
        'Payment order raised with the gateway. The client now pays through Razorpay — this does not mark the invoice paid.',
      )
    },
    onError: (error) => {
      toast.error(extractApiErrorMessage(error, 'Could not raise the payment order.'))
    },
  })
}

/**
 * PATCH /api/v1/payment-orders/{id}/cancel — no body.
 *
 * CANCELLING NOW CLOSES THE GATEWAY ORDER. The handler closes the Razorpay
 * order first and only then writes the local status, and if the gateway close
 * fails the whole operation is refused (a 5xx surfaces the provider's refusal
 * via extractApiErrorMessage) with the local order left untouched and still
 * payable. So a successful cancel means the money cannot be taken on this order
 * any more, and the toast may say so plainly.
 */
export function useCancelAdminPaymentOrder() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: (paymentOrderId) => cancelAdminPaymentOrder(paymentOrderId),
    onSuccess: (cancelled) => {
      // `invoiceType` here is the enum from CancelPaymentOrderResponseDto, so it
      // arrives as a string; the id column the status refers to is read from
      // whichever of the two is populated. The type only decides which invoice list
      // prefix to refresh, so the one-off case of a response arriving without a type
      // falls back to the retainer list rather than skipping the refresh.
      const linkedInvoiceId =
        cancelled?.retainerInvoiceId ?? cancelled?.serviceFeeInvoiceId ?? null
      const resolvedKind = invoiceKindForPaymentType(cancelled?.invoiceType)

      invalidateAll(queryClient, [
        ...ALWAYS_INVALIDATED,
        PAYMENT_LIST_KEY,
        PAYMENT_DETAIL_KEY,
        invoiceListKey(resolvedKind),
        ...(linkedInvoiceId ? [invoiceDetailKey(resolvedKind, linkedInvoiceId)] : []),
      ])

      toast.success(
        'Payment order cancelled and closed with the payment provider. It can no longer be paid.',
      )
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not cancel this payment order.'),
      )
    },
  })
}
