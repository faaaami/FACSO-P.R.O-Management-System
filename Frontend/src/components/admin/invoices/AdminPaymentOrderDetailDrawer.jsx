import { useEffect, useId, useRef, useState } from 'react'
import { CreditCard, ScrollText, X, XCircle } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import ConfirmDialog from '../../shared/ConfirmDialog'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminPaymentOrder } from '../../../hooks/admin/useAdminPayments'
import { useCancelAdminPaymentOrder } from '../../../hooks/admin/useAdminBillingMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminBillingActivitySection from './AdminBillingActivitySection'
import AdminDetailRow from '../clients/AdminDetailRow'
import {
  BILLING_ACTION_CONFIRMS,
  BILLING_ACTION_LABELS,
  BILLING_AUDIT_ENTITY_TYPES,
  BILLING_LIFECYCLE_NOTE,
  IRREVERSIBLE_ACTIONS,
  NO_MONEY_AGGREGATION_NOTE,
  PAYMENT_CANCEL_CONSEQUENCE_NOTE,
  PAYMENT_CANCEL_ELIGIBILITY_NOTE,
  PAYMENT_CANCEL_GATEWAY_NOTE,
  PAYMENT_CANCEL_NOTE,
  PAYMENT_NO_CAPTURE_NOTE,
  PAYMENT_STATUS_FILTER_COPY,
  dateTimeText,
  gatewayOrderText,
  linkedInvoiceReference,
  moneyParts,
  paymentInvoiceTypeText,
  paymentOrderStatusText,
  paymentOrderStatusTone,
  presentText,
  shortGuid,
} from './billingDisplay'

/**
 * Detail drawer for one payment order, from GetPaymentOrderByIdResponseDto.
 *
 * ------------------------------------------------------------------
 * THE DETAIL DTO IS THE LIST DTO PLUS `razorpayPaymentId`, and that one field is
 * why this drawer exists rather than reusing the list row: a Razorpay PAYMENT id is
 * only present once the gateway has actually confirmed a payment, so it is the
 * single most useful thing an administrator can learn from opening a payment order,
 * and the list does not carry it. The detail is therefore fetched on open even
 * though the two shapes otherwise agree.
 *
 * ------------------------------------------------------------------
 * TWO NUMERIC ENUMS. `invoiceType` and `status` are declared `int` on the DTO, so
 * they arrive as numbers and the maps in billingDisplay are keyed numerically. They
 * are looked up WITHOUT String() coercion, so no value is ever turned into a key
 * that may not exist, and an unmapped ordinal shows as itself rather than as a
 * confident wrong label.
 *
 * ------------------------------------------------------------------
 * WHAT IS STILL DELIBERATELY ABSENT.
 * ------------------------------------------------------------------
 *   - NO PAYMENT STATUS FILTER, on the list or here. The repository cannot execute
 *     it; see useAdminPayments.js.
 *   - NO PDF. The payment-orders controller exposes no document route.
 *   - NO LINKED-INVOICE LINK. The detail carries `retainerInvoiceId` /
 *     `serviceFeeInvoiceId`, and no Admin route accepts an invoice id, so the id is
 *     shown as a reference rather than as a link to nowhere.
 *   - NO CAPTURE, REFUND OR GATEWAY CLOSE. Nothing in this portal takes the money;
 *     settling an order is the gateway webhook's job.
 *
 * ------------------------------------------------------------------
 * CANCEL IS OFFERED, AND ONLY ON Created (1) or Pending (2).
 * ------------------------------------------------------------------
 * The old note here said cancel was withheld because a control for it "would be an
 * action this page cannot offer an undo for" — true of the undo, and now handled by
 * confirming it. What has NOT changed is how narrow the action is, and the
 * confirmation is deliberately long about it, because "Cancel" on a payment order
 * invites a reading it does not deserve.
 *
 * CancelPaymentOrderCommandHandler sets Status = Cancelled and CancelledAt and
 * writes the audit row. That is ALL it does. It does not call Razorpay, does not
 * clear or close the gateway order, and does not touch the linked invoice — so a
 * Pending invoice stays Pending. Three consequences follow, and all three are stated
 * before the reader confirms:
 *   1. the invoice is unchanged and still unpaid;
 *   2. the invoice can never get a new payment order through this path, because the
 *      duplicate guard tests the gateway reference and cancelling keeps it;
 *   3. a client who still pays the open gateway order settles it at the gateway
 *      while this record reads Cancelled.
 *
 * A `Paid` order is not cancellable and shows no control: the gateway has already
 * settled it, and the server refuses with 409.
 *
 * The button is NOT placed on the invoice drawer's action area or anywhere else —
 * cancel belongs to the ORDER, and this is the order's only surface.
 *
 * TWO TABS, ACTIVITY LAZY, ONE FOCUS TRAP, ONE SCROLL CONTAINER, NO NESTED DRAWER —
 * the same contract as every other Admin drawer. See AdminInvoiceDetailDrawer for
 * why the panel's aria-labelledby falls back to the drawer heading while loading.
 *
 * The Activity tab is worth reading carefully: a completed payment is recorded by
 * the gateway webhook handler, not by any Admin action, so a Paid order here will
 * show a webhook entry this UI never produced.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: CreditCard },
  { key: 'activity', label: 'Activity (audit log)', icon: ScrollText },
]

function AdminPaymentOrderDetailDrawer({ paymentOrderId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const tabRefs = useRef([])
  const [isConfirmingCancel, setIsConfirmingCancel] = useState(false)
  const [cancelError, setCancelError] = useState(null)

  const { data: payment, isLoading, isError, error, refresh } =
    useAdminPaymentOrder(paymentOrderId)

  const cancelOrder = useCancelAdminPaymentOrder()

  // A different order is a different record, so a staged cancellation is discarded
  // rather than left armed against it.
  useEffect(() => {
    setActiveTab('overview')
    setIsConfirmingCancel(false)
    setCancelError(null)
  }, [paymentOrderId])

  const linked = linkedInvoiceReference(payment)
  const money = moneyParts(payment?.amount, payment?.currency)
  const notes = presentText(payment?.notes)
  const gatewayOrder = gatewayOrderText(payment?.razorpayOrderId)
  const gatewayPayment = gatewayOrderText(payment?.razorpayPaymentId)
  const typeLabel = paymentInvoiceTypeText(payment?.invoiceType)

  const heading = payment
    ? `${typeLabel ?? 'Payment order'} · ${shortGuid(payment.id) ?? ''}`.trim()
    : 'Loading payment order…'

  /**
   * CANCELLABILITY IS DECIDED BY THE NUMERIC STATUS, because that is how it arrives:
   * `PaymentStatus.Created = 1` and `PaymentStatus.Pending = 2`, and the DTO declares
   * the field as `int`. So the comparison is against numbers and is not coerced to a
   * string — a value arriving as "2" would fail the test and hide the control rather
   * than mislabel it, which is the safe direction to be wrong in.
   */
  const canCancel = payment?.status === 1 || payment?.status === 2
  const cancelCopy = BILLING_ACTION_CONFIRMS[IRREVERSIBLE_ACTIONS.PAYMENT_CANCEL]

  function confirmCancel() {
    setCancelError(null)

    cancelOrder.mutate(payment.id, {
      onSuccess: () => {
        // The drawer stays open so the new status and CancelledAt are visible, and
        // so the reader can see the gateway reference is still there — which is the
        // consequence the confirmation warned about.
        setIsConfirmingCancel(false)
      },
      onError: (requestError) => {
        setIsConfirmingCancel(false)
        setCancelError(
          extractApiErrorMessage(
            requestError,
            'Could not cancel this payment order.',
          ),
        )
      },
    })
  }

  function handleTabKeyDown(event) {
    const currentIndex = TABS.findIndex((tab) => tab.key === activeTab)
    let nextIndex = null

    if (event.key === 'ArrowRight') nextIndex = (currentIndex + 1) % TABS.length
    else if (event.key === 'ArrowLeft') {
      nextIndex = (currentIndex - 1 + TABS.length) % TABS.length
    } else if (event.key === 'Home') nextIndex = 0
    else if (event.key === 'End') nextIndex = TABS.length - 1

    if (nextIndex == null) return

    event.preventDefault()
    setActiveTab(TABS[nextIndex].key)
    tabRefs.current[nextIndex]?.focus()
  }

  function renderTab() {
    if (activeTab === 'activity') {
      return (
        <AdminBillingActivitySection
          entityType={BILLING_AUDIT_ENTITY_TYPES.payment}
          entityId={payment.id}
        />
      )
    }

    return (
      <div className="flex flex-col gap-5">
        <section
          aria-labelledby="admin-payment-order-overview-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-payment-order-overview-heading"
            className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
          >
            <CreditCard size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
            Payment order
          </h3>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <StatusPill
              label={paymentOrderStatusText(payment.status) ?? 'Unknown'}
              tone={paymentOrderStatusTone(payment.status)}
            />
            <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-medium text-[#6B7280]">
              {typeLabel ?? 'Unknown invoice type'}
            </span>
          </div>

          <dl className="mt-3.5 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            <AdminDetailRow label="Client company" value={payment.clientCompanyName} />
            <AdminDetailRow label="Payment order ID" value={payment.id} mono />

            {/*
              Both nullable invoice-id columns are shown, and the one that is empty
              says so. Reporting only the populated one would hide which kind of
              invoice this order is for, and the DTO's nullability is the only
              evidence of that.
            */}
            <AdminDetailRow label="Retainer invoice ID" value={payment.retainerInvoiceId} mono />
            <AdminDetailRow label="Service Fee invoice ID" value={payment.serviceFeeInvoiceId} mono />

            {linked.present ? (
              <AdminDetailRow label="Linked invoice" value={linked.label} />
            ) : null}

            <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
              <dt className="text-xs font-medium text-[#6B7280]">Amount</dt>
              <dd className="min-w-0 text-sm text-[#16181D]">
                <span className="tabular-nums">{money.formatted}</span>
                <span className="ml-2 text-xs text-[#6B7280]">
                  {money.hasCurrency
                    ? `Currency: ${money.rawCurrency}`
                    : 'Currency not recorded'}
                </span>
              </dd>
            </div>
          </dl>
        </section>

        <section
          aria-labelledby="admin-payment-order-gateway-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-payment-order-gateway-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Gateway
          </h3>

          <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            {/*
              Both are Razorpay's own free-text references, not Guids, so they are
              printed whole. A truncated gateway reference would still look like a
              complete identifier while being unusable.
            */}
            <AdminDetailRow label="Razorpay order ID" value={gatewayOrder} mono />
            <AdminDetailRow label="Razorpay payment ID" value={gatewayPayment} mono />
          </dl>

          {payment.razorpayPaymentId ? (
            <p className="mt-2.5 text-xs text-[#6B7280]">
              A payment id is present, so the gateway has returned a payment against
              this order.
            </p>
          ) : (
            <p className="mt-2.5 text-xs text-[#9CA3AF]">
              No payment id is recorded. One is only written once the gateway confirms
              a payment, and that confirmation arrives through a webhook rather than
              through anything in this portal.
            </p>
          )}
        </section>

        <section
          aria-labelledby="admin-payment-order-timeline-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-payment-order-timeline-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Timeline
          </h3>

          <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            <AdminDetailRow label="Created" value={dateTimeText(payment.createdAt)} />
            <AdminDetailRow label="Last updated" value={dateTimeText(payment.updatedAt)} />
            <AdminDetailRow label="Paid" value={dateTimeText(payment.paidAt)} />
            <AdminDetailRow label="Failed" value={dateTimeText(payment.failedAt)} />
            <AdminDetailRow label="Cancelled" value={dateTimeText(payment.cancelledAt)} />
          </dl>
        </section>

        {notes && (
          <section
            aria-labelledby="admin-payment-order-notes-heading"
            className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
          >
            <h3
              id="admin-payment-order-notes-heading"
              className="text-sm font-semibold tracking-tight text-[#16181D]"
            >
              Notes
            </h3>
            <p className="mt-2.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
              {notes}
            </p>
          </section>
        )}

        {/*
          This section previously read "Read-only record" and said no action was
          offered. Both claims are now false — an order can be cancelled from the
          footer above — so the heading, the copy and the id all change together. The
          two paragraphs that remain are the two limitations that still hold: the
          unusable status filter, and the absence of money aggregation.
        */}
        <section
          aria-labelledby="admin-payment-order-lifecycle-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
        >
          <h3
            id="admin-payment-order-lifecycle-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Record lifecycle
          </h3>

          <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
            {BILLING_LIFECYCLE_NOTE} {PAYMENT_NO_CAPTURE_NOTE}
          </p>
          <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
            {PAYMENT_STATUS_FILTER_COPY} {NO_MONEY_AGGREGATION_NOTE}
          </p>
        </section>
      </div>
    )
  }

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className="fixed inset-y-0 right-0 z-50 flex w-full max-w-[680px] flex-col border-l border-[#E2E4E9] bg-white shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none"
      >
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] p-5 sm:p-6">
          <div className="flex min-w-0 items-start gap-3">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#16181D]">
              <CreditCard size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !payment ? 'Loading payment order…' : heading}
              </h2>
              {payment && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill
                    label={paymentOrderStatusText(payment.status) ?? 'Unknown'}
                    tone={paymentOrderStatusTone(payment.status)}
                  />
                  <span className="min-w-0 break-words font-mono text-xs text-[#6B7280]">
                    {shortGuid(payment.id)}
                  </span>
                </div>
              )}
            </div>
          </div>

          <button
            type="button"
            onClick={onClose}
            aria-label={`Close ${heading} details`}
            className="shrink-0 cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        {!isLoading && !isError && (
          <div
            role="tablist"
            aria-label="Payment order sections"
            onKeyDown={handleTabKeyDown}
            className="flex gap-1 overflow-x-auto border-b border-[#E2E4E9] px-3 py-2 sm:px-4"
          >
            {TABS.map((tab, index) => {
              const Icon = tab.icon
              const isActive = tab.key === activeTab

              return (
                <button
                  key={tab.key}
                  ref={(node) => {
                    tabRefs.current[index] = node
                  }}
                  type="button"
                  role="tab"
                  id={`${titleId}-tab-${tab.key}`}
                  aria-selected={isActive}
                  aria-controls={`${titleId}-panel`}
                  tabIndex={isActive ? 0 : -1}
                  onClick={() => setActiveTab(tab.key)}
                  className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap rounded-[8px] px-2.5 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] ${
                    isActive
                      ? 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74]'
                      : 'text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D]'
                  }`}
                >
                  <Icon size={14} strokeWidth={1.75} aria-hidden="true" />
                  {tab.label}
                </button>
              )
            })}
          </div>
        )}

        {/* The single scroll container for the whole panel. */}
        <div
          id={`${titleId}-panel`}
          role="tabpanel"
          aria-labelledby={isLoading || isError ? titleId : `${titleId}-tab-${activeTab}`}
          tabIndex={0}
          className="flex-1 overflow-y-auto p-5 sm:p-6"
        >
          {isLoading && <LoadingState label="Loading payment order…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(error, 'Could not load this payment order.')}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && payment && renderTab()}

          {!isLoading && !isError && !payment && (
            <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
              The request succeeded but returned no payment order record, so there
              is nothing to display. The record may have been deleted.
            </p>
          )}
        </div>

        {/*
          THE ACTION AREA, RENDERED ONLY ON Created AND Pending. A Paid order is not
          cancellable — the gateway has already settled it and the server answers 409 —
          so this footer is absent rather than showing a control that cannot work.

          The restating of the consequence below the button is not decoration. The
          button label is a two-word verb, and the reader who has just seen
          "no capture, refund, or gateway close" above is owed the specific reason
          that matters for a payment order before they commit: the linked invoice
          does not move.
        */}
        {!isLoading && !isError && payment && canCancel && (
          <div className="border-t border-[#E2E4E9] p-5 sm:p-6">
            {cancelError && (
              <div
                role="alert"
                className="mb-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
              >
                <p className="text-xs font-medium text-[#DC2626]">{cancelError}</p>
              </div>
            )}

            <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={() => setIsConfirmingCancel(true)}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                <XCircle size={15} strokeWidth={2} aria-hidden="true" />
                {BILLING_ACTION_LABELS[IRREVERSIBLE_ACTIONS.PAYMENT_CANCEL]}
              </button>
            </div>

            <p className="mt-3 text-xs leading-relaxed text-[#6B7280]">
              {PAYMENT_CANCEL_NOTE}
            </p>
            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              {PAYMENT_CANCEL_GATEWAY_NOTE}
            </p>
          </div>
        )}
      </aside>

      {/*
        CANCEL CONFIRMATION.

        The message is assembled from the shared constant rather than written here, so
        the words that describe this irreversible, gateway-closing change live in one
        place and cannot drift from the copy the invoices drawers show for their own
        irreversible actions.

        `cancelCopy.tone` is 'danger': the same tone as void, because from the money's
        point of view this is the same kind of mistake to recover from.
      */}
      <ConfirmDialog
        open={isConfirmingCancel}
        title={cancelCopy.title}
        message={[
          cancelCopy.irreversible,
          PAYMENT_CANCEL_CONSEQUENCE_NOTE,
          PAYMENT_CANCEL_GATEWAY_NOTE,
          cancelOrder.isPending ? 'This cancellation is in progress.' : '',
        ]
          .filter(Boolean)
          .join(' ')}
        confirmLabel={cancelCopy.confirmLabel}
        tone={cancelCopy.tone}
        isLoading={cancelOrder.isPending}
        onConfirm={confirmCancel}
        onCancel={() => setIsConfirmingCancel(false)}
      />
    </div>
  )
}

export default AdminPaymentOrderDetailDrawer
