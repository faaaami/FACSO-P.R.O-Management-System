import { useEffect, useId, useRef, useState } from 'react'
import { Ban, CheckCircle2, FileText, Inbox, ScrollText, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import ConfirmDialog from '../../shared/ConfirmDialog'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminRetainerInvoice, useAdminServiceFeeInvoice } from '../../../hooks/admin/useAdminInvoices'
import {
  useMarkAdminRetainerInvoicePaid,
  useMarkAdminServiceFeeInvoicePaid,
  useVoidAdminRetainerInvoice,
  useVoidAdminServiceFeeInvoice,
} from '../../../hooks/admin/useAdminBillingMutations'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminInvoiceOverviewSection from './AdminInvoiceOverviewSection'
import AdminBillingActivitySection from './AdminBillingActivitySection'
import {
  BILLING_ACTION_CONFIRMS,
  BILLING_ACTION_LABELS,
  BILLING_AUDIT_ENTITY_TYPES,
  IRREVERSIBLE_ACTIONS,
  MARK_PAID_CANCELLED_ORDER_NOTE,
  MARK_PAID_SIDE_EFFECT_NOTE,
  VOID_REASON_HINT,
  VOID_SIDE_EFFECT_NOTE,
  retainerStatusText,
  retainerStatusTone,
  serviceFeeStatusText,
  serviceFeeStatusTone,
  shortGuid,
} from './billingDisplay'

/**
 * Detail drawer for BOTH invoice types.
 *
 * ONE DRAWER, TWO KINDS. The Retainer and Service Fee detail records share a shape
 * and a workflow, and rendering them through one drawer means the two tabs behave
 * identically — same tabs, same lazy loading, same focus handling. `kind` is not a
 * styling switch: it selects a different endpoint, a different query key, a
 * different status enum and a different DTO, all of which are branched on
 * explicitly. The overview section receives the same `kind` and renders only the
 * fields that kind's DTO actually has.
 *
 * ------------------------------------------------------------------
 * TWO TABS, ONE OF THEM LAZY.
 * ------------------------------------------------------------------
 * The detail loads on open because the header shows the status. Activity mounts its
 * audit query ONLY when that tab is first activated, so opening the drawer costs one
 * request. A hook call cannot be skipped, so the section is not rendered at all
 * until the tab is active — which is what makes the fetch lazy rather than merely
 * hidden.
 *
 * ------------------------------------------------------------------
 * NO NESTED DRAWER, ONE FOCUS TRAP, ONE SCROLL CONTAINER.
 * ------------------------------------------------------------------
 * Built on the shared useFocusTrap exactly as the Admin Tasks, Documents, Employees
 * and Service Requests drawers are, so Escape, bidirectional Tab wrap, focus-on-open,
 * focus restoration to the triggering row and body scroll lock behave identically
 * across the Admin modules. The hook is used unmodified; overlay click-to-close is
 * wired here because the hook does not provide it.
 *
 * The only outbound surface besides the two write actions below is the PDF, which
 * opens a browser tab and therefore leaves the trap entirely.
 *
 * ------------------------------------------------------------------
 * THE ACTION AREA, AND WHY IT IS OFFERED ONLY ON A PENDING INVOICE.
 * ------------------------------------------------------------------
 * Both write actions are gated on `Pending` — and by the SERVER, not by this UI's
 * preference: MarkRetainerInvoicePaid, VoidRetainerInvoice, MarkServiceFeeInvoicePaid
 * and VoidServiceFeeInvoice all raise 409 "Only pending invoices can be…" otherwise.
 * So a Paid or Void invoice renders no action area at all, rather than buttons that
 * would fail. Terminal is terminal, and offering "Mark as paid" on a paid invoice
 * would be a control whose only possible outcome is an error.
 *
 * VOID TAKES A REASON, AND THE REASON IS OPTIONAL. Both void requests take a single
 * nullable `string? Reason` that the handler stores as `Reason?.Trim()`, and neither
 * validator requires it — so the field is offered, explained as optional, and an
 * empty submission is sent as null rather than blocked. Requiring it would be the
 * frontend inventing a rule the server does not have.
 *
 * THE REASON IS COLLECTED IN THE DRAWER'S OWN ACTION AREA RATHER THAN IN A SECOND
 * DIALOG, and then confirmed. The approved manifest for this phase allows six new
 * files and none of them is a void-reason dialog, so the prompt lives in the footer
 * as a revealed panel and the irreversible step is still a ConfirmDialog. The
 * sequence is Void -> type (or skip) the reason -> Review -> confirm, and the
 * confirmation's message quotes the reason that will be recorded, so what is about to
 * be written is visible before it is written.
 *
 * NEITHER ACTION IS OPTIMISTIC. Both are one-way with no reversing route, so each
 * waits for the server and then invalidates; see useAdminBillingMutations. The
 * success and error toasts belong to that hook, and what stays here is the error
 * banner, which a toast cannot replace because the drawer is still open.
 *
 * THE HEADER'S FAILURE IS TOTAL. With no status there is nothing coherent to render
 * above the tabs, so a failed detail request is a full-drawer error with a retry and
 * the tablist is not rendered at all. Activity then fails on its own terms and
 * leaves the rest of the drawer usable.
 *
 * aria-labelledby on the tabpanel points at the active tab, but the tablist only
 * exists once the detail has loaded — so during the first load and on error the
 * panel is labelled by the drawer heading instead, and never references an element
 * that is not in the DOM.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: FileText },
  { key: 'activity', label: 'Activity (audit log)', icon: Inbox },
]

function AdminInvoiceDetailDrawer({ kind, invoiceId, onClose }) {
  const titleId = useId()
  const voidReasonId = useId()
  const actionErrorId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const tabRefs = useRef([])

  /**
   * `pendingAction` names which irreversible action is staged for confirmation, or
   * null when none is. It is a single field rather than a boolean per action so the
   * two can never both be armed, and so "Cancel" has exactly one meaning.
   *
   * `voidReason` is local form state rather than a React Hook Form instance: it is a
   * single optional textarea whose value is only needed at submit time, and a full
   * form here would be a resolver and a validation cycle for one unvalidated field.
   */
  const [pendingAction, setPendingAction] = useState(null)
  const [voidReason, setVoidReason] = useState('')
  const [actionError, setActionError] = useState(null)

  const isRetainer = kind === 'retainer'

  // Both hooks are called on every render and exactly one resolves: the other is
  // disabled by its own `enabled: Boolean(id)` because the id it would receive is
  // not passed. A conditional hook call is not an option.
  const retainer = useAdminRetainerInvoice(isRetainer ? invoiceId : undefined)
  const serviceFee = useAdminServiceFeeInvoice(isRetainer ? undefined : invoiceId)

  const { data: invoice, isLoading, isError, error, refresh } = isRetainer
    ? retainer
    : serviceFee

  /**
   * BOTH MUTATION HOOKS ARE ALWAYS CALLED, and the one for the other invoice type
   * receives `undefined` and is therefore never invoked. This mirrors how the two
   * read hooks above are handled: a conditional hook call is not an option in React.
   */
  const markRetainerPaid = useMarkAdminRetainerInvoicePaid()
  const voidRetainer = useVoidAdminRetainerInvoice()
  const markServiceFeePaid = useMarkAdminServiceFeeInvoicePaid()
  const voidServiceFee = useVoidAdminServiceFeeInvoice()

  const markPaidMutation = isRetainer ? markRetainerPaid : markServiceFeePaid
  const voidMutation = isRetainer ? voidRetainer : voidServiceFee

  // A different invoice means a different record entirely, so any staged action,
  // typed reason or error from the previous one is discarded rather than left armed
  // against a record the reader has moved on from.
  useEffect(() => {
    setActiveTab('overview')
    setPendingAction(null)
    setVoidReason('')
    setActionError(null)
  }, [invoiceId, kind])

  const noun = isRetainer ? 'retainer invoice' : 'Service Fee invoice'
  const statusLabel = isRetainer
    ? retainerStatusText(invoice?.status)
    : serviceFeeStatusText(invoice?.status)
  const statusTone = isRetainer
    ? retainerStatusTone(invoice?.status)
    : serviceFeeStatusTone(invoice?.status)

  const heading = invoice
    ? `${invoice.invoiceNumber ?? noun} · ${shortGuid(invoice.id) ?? ''}`.trim()
    : `Loading ${noun}…`

  /**
   * BOTH WRITES REQUIRE `Pending`, so the action area exists only in that state.
   * The comparison is against the enum NAME, which is how the value arrives.
   */
  const isPendingInvoice = invoice?.status === 'Pending'
  const isActing = markPaidMutation.isPending || voidMutation.isPending

  /**
   * A LINKED ORDER OF `Cancelled` MAKES MARK-PAID INAPPLICABLE, so the button is
   * withheld rather than disabled — a greyed-out button reads as "not right now"
   * and invites a retry that can only fail again. Void is still offered: a pending
   * invoice with a cancelled order is exactly what voiding cleans up.
   *
   * Matched on the name and the ordinal both, because `PaymentStatus` is rendered
   * as a string by the global converter but arrives numeric on any response that
   * escapes it.
   */
  const hasCancelledOrder =
    invoice?.paymentOrderStatus === 'Cancelled' || invoice?.paymentOrderStatus === 5
  const canMarkPaid = isPendingInvoice && !hasCancelledOrder

  const markPaidCopy = BILLING_ACTION_CONFIRMS[IRREVERSIBLE_ACTIONS.MARK_PAID]
  const voidCopy = BILLING_ACTION_CONFIRMS[IRREVERSIBLE_ACTIONS.VOID]

  function cancelStagedAction() {
    setPendingAction(null)
    setVoidReason('')
    setActionError(null)
  }

  function stageAction(action) {
    setActionError(null)
    setPendingAction(action)
  }

  function confirmMarkPaid() {
    setActionError(null)

    markPaidMutation.mutate(invoice.id, {
      onSuccess: () => {
        // The drawer stays open on purpose: the reader has just changed a money
        // record and will want to see the new status and the audit row behind it.
        // The list behind the drawer is already refreshed by the invalidation.
        setPendingAction(null)
      },
      onError: (requestError) => {
        setPendingAction(null)
        setActionError(
          extractApiErrorMessage(requestError, 'Could not mark this invoice as paid.'),
        )
      },
    })
  }

  function confirmVoid() {
    setActionError(null)

    // Trimmed, and an empty string becomes null. The wrapper sends null for a falsy
    // reason, which is exactly what the nullable string binds — and it means a
    // reason of pure whitespace records as "no reason" rather than as a blank one.
    const reason = voidReason.trim() ? voidReason.trim() : null

    voidMutation.mutate(
      { invoiceId: invoice.id, reason },
      {
        onSuccess: () => {
          setPendingAction(null)
          setVoidReason('')
        },
        onError: (requestError) => {
          setPendingAction(null)
          setActionError(
            extractApiErrorMessage(requestError, 'Could not void this invoice.'),
          )
        },
      },
    )
  }

  /**
   * Arrow-key navigation. A tablist that only responds to clicks is unusable by
   * keyboard: Left/Right move between tabs, Home/End jump to the ends, and focus
   * follows the selection.
   */
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

  /**
   * Only the active tab is mounted, which is what makes the lazy fetching lazy: the
   * Activity section runs its own audit query, so an unvisited tab issues nothing.
   */
  function renderTab() {
    if (activeTab === 'activity') {
      return (
        <AdminBillingActivitySection
          entityType={BILLING_AUDIT_ENTITY_TYPES[kind]}
          entityId={invoice.id}
        />
      )
    }

    return <AdminInvoiceOverviewSection kind={kind} invoice={invoice} />
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
              <FileText size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !invoice ? `Loading ${noun}…` : heading}
              </h2>
              {invoice && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill label={statusLabel ?? 'Unknown'} tone={statusTone} />
                  <span className="min-w-0 break-words font-mono text-xs text-[#6B7280]">
                    {shortGuid(invoice.id)}
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
            aria-label={`${isRetainer ? 'Retainer invoice' : 'Service Fee invoice'} sections`}
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
          {isLoading && <LoadingState label={`Loading ${noun}…`} />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(error, `Could not load this ${noun}.`)}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && invoice && renderTab()}

          {/*
            A detail that resolved but carried no usable body is a contract problem,
            not an empty invoice. It is reported as such rather than as a blank panel,
            which would read as "nothing to show".
          */}
          {!isLoading && !isError && !invoice && (
            <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
              The request succeeded but returned no invoice record, so there is
              nothing to display. The record may have been deleted.
            </p>
          )}

          {!isLoading && !isError && invoice && activeTab === 'overview' ? (
            <p className="mt-4 flex items-start gap-1.5 text-[11px] leading-relaxed text-[#9CA3AF]">
              <ScrollText size={12} strokeWidth={1.75} className="mt-0.5 shrink-0" aria-hidden="true" />
              <span>
                The Activity tab reads the platform-wide audit log for this record.
                It has not been requested yet, and loads only when that tab is
                opened.
              </span>
            </p>
          ) : null}
        </div>

        {/*
          THE ACTION AREA. Rendered only for a PENDING invoice, because both writes
          are refused by the server otherwise. On a Paid or Void record this renders
          nothing at all — not a disabled pair of buttons, which would imply the
          actions are temporarily unavailable rather than permanently inapplicable.
        */}
        {!isLoading && !isError && invoice && isPendingInvoice && (
          <div className="border-t border-[#E2E4E9] p-5 sm:p-6">
            {actionError && (
              <div
                id={actionErrorId}
                role="alert"
                className="mb-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
              >
                <p className="text-xs font-medium text-[#DC2626]">{actionError}</p>
              </div>
            )}

            {pendingAction === 'void' ? (
              <div className="flex flex-col gap-3">
                <div>
                  <label
                    htmlFor={voidReasonId}
                    className="block text-sm font-medium text-[#16181D]"
                  >
                    Void reason (optional)
                  </label>
                  <textarea
                    id={voidReasonId}
                    rows={3}
                    value={voidReason}
                    onChange={(event) => setVoidReason(event.target.value)}
                    disabled={isActing}
                    aria-describedby={voidReasonId ? `${voidReasonId}-hint` : undefined}
                    className="mt-1.5 block w-full resize-y rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:bg-gray-50 disabled:text-[#9CA3AF]"
                  />
                  <p
                    id={`${voidReasonId}-hint`}
                    className="mt-1.5 text-xs leading-relaxed text-[#6B7280]"
                  >
                    {VOID_REASON_HINT}
                  </p>
                </div>

                <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                  <button
                    type="button"
                    onClick={cancelStagedAction}
                    disabled={isActing}
                    className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Cancel
                  </button>
                  <button
                    type="button"
                    onClick={() => stageAction('void-review')}
                    disabled={isActing}
                    className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#DC2626] px-4 py-2.5 text-sm font-semibold text-white transition duration-150 hover:bg-[#B91C1C] focus:outline-none focus:ring-2 focus:ring-[#DC2626] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <Ban size={15} strokeWidth={2} aria-hidden="true" />
                    Review void
                  </button>
                </div>
              </div>
            ) : (
              <div className="flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
                <button
                  type="button"
                  onClick={() => stageAction('void')}
                  disabled={isActing}
                  className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
                >
                  <Ban size={15} strokeWidth={2} aria-hidden="true" />
                  {BILLING_ACTION_LABELS[IRREVERSIBLE_ACTIONS.VOID]}
                </button>
                {canMarkPaid && (
                  <button
                    type="button"
                    onClick={() => stageAction('mark-paid')}
                    disabled={isActing}
                    className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    <CheckCircle2 size={15} strokeWidth={2} aria-hidden="true" />
                    {BILLING_ACTION_LABELS[IRREVERSIBLE_ACTIONS.MARK_PAID]}
                  </button>
                )}
              </div>
            )}
          </div>
        )}

        {isPendingInvoice && hasCancelledOrder && (
          <div className="border-t border-[#E2E4E9] p-5 sm:p-6">
            <div className="rounded-[10px] border border-[#FDE68A] bg-[#FFFBEB] px-4 py-3 text-xs leading-relaxed text-[#92400E]">
              {MARK_PAID_CANCELLED_ORDER_NOTE}
            </div>
          </div>
        )}
      </aside>

      {/*
        Mark-paid confirmation. The message names the second record it changes,
        because the handler sets the linked payment order to Paid in the same
        transaction — a confirmation describing it as a single-record edit would
        understate what the button does.
      */}
      <ConfirmDialog
        open={pendingAction === 'mark-paid'}
        title={markPaidCopy.title}
        message={[
          `Invoice ${presentInvoiceNumber(invoice)}.`,
          markPaidCopy.irreversible,
  MARK_PAID_SIDE_EFFECT_NOTE,
  MARK_PAID_CANCELLED_ORDER_NOTE,
        ].join(' ')}
        confirmLabel={markPaidCopy.confirmLabel}
        tone={markPaidCopy.tone}
        isLoading={markPaidMutation.isPending}
        onConfirm={confirmMarkPaid}
        onCancel={cancelStagedAction}
      />

      {/*
        Void confirmation. The reason is quoted INSIDE the confirmation, so what is
        about to be written is visible at the moment it is agreed to — and the copy
        says plainly that leaving it blank records no reason at all. It also names
        the gateway close, because voiding is no longer an invoice-only edit.
      */}
      <ConfirmDialog
        open={pendingAction === 'void-review'}
        title={voidCopy.title}
        message={[
          `Invoice ${presentInvoiceNumber(invoice)}.`,
          voidCopy.irreversible,
          VOID_SIDE_EFFECT_NOTE,
          voidReason.trim()
            ? `The reason recorded will be: "${voidReason.trim()}"`
            : 'No reason was given, so none will be recorded.',
        ].join(' ')}
        confirmLabel={voidCopy.confirmLabel}
        tone={voidCopy.tone}
        isLoading={voidMutation.isPending}
        onConfirm={confirmVoid}
        onCancel={cancelStagedAction}
      />
    </div>
  )
}

/** An invoice's number for prose, falling back to its short id rather than to null. */
function presentInvoiceNumber(invoice) {
  const number = typeof invoice?.invoiceNumber === 'string' ? invoice.invoiceNumber.trim() : ''
  if (number) return number
  return shortGuid(invoice?.id) ?? 'this invoice'
}

export default AdminInvoiceDetailDrawer
