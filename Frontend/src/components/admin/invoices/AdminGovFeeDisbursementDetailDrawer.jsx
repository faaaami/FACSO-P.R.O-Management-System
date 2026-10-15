import { useEffect, useId, useRef, useState } from 'react'
import { ArrowRight, Landmark, ScrollText, X } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useAdminGovFeeDisbursement } from '../../../hooks/admin/useAdminInvoices'
import { extractApiErrorMessage } from '../../../utils/apiError'
import AdminBillingActivitySection from './AdminBillingActivitySection'
import AdminGovFeeDisbursementStatusDialog from './AdminGovFeeDisbursementStatusDialog'
import AdminDetailRow from '../clients/AdminDetailRow'
import {
  BILLING_ACTION_LABELS,
  BILLING_AUDIT_ENTITY_TYPES,
  GOV_FEE_TERMINAL_NOTE,
  IRREVERSIBLE_ACTIONS,
  NO_MONEY_AGGREGATION_NOTE,
  BILLING_LIFECYCLE_NOTE,
  dateTimeText,
  formatDateTime,
  govFeeStatusText,
  govFeeStatusTone,
  govFeeTaskReference,
  moneyParts,
  nextGovFeeStatus,
  presentText,
  shortGuid,
} from './billingDisplay'

/**
 * Detail drawer for one government-fee disbursement, from
 * GetGovFeeDisbursementByIdResponseDto.
 *
 * ------------------------------------------------------------------
 * IT HAS ITS OWN DRAWER BECAUSE IT IS NOT AN INVOICE.
 * ------------------------------------------------------------------
 * A disbursement is the firm having paid a government fee and waiting to be
 * reimbursed. There is no invoice number, no due date, no void state and no void
 * reason on the DTO, so none of those rows exists here — not as a dash, which would
 * mean "recorded as empty", but as nothing at all. A reader who saw an "Invoice
 * number —" on a disbursement would conclude the record was incomplete.
 *
 * Its own status enum is likewise its own: PaidByFirm, InvoicedToClient,
 * Reimbursed. `InvoicedToClient` is a real state on this record — the firm HAS
 * billed the client — but it is a timestamp here, not an invoice, and the timeline
 * below is the disbursement's own.
 *
 * THE DETAIL IS FETCHED EVEN THOUGH THE LIST ROW ALREADY LOOKS IDENTICAL.
 * `GovFeeDisbursementDto` and `GetGovFeeDisbursementByIdResponseDto` carry the same
 * fields, so the detail request could be skipped and the list row shown instead.
 * It is not: the list row is a snapshot that may already be stale, the detail
 * endpoint is the authoritative read and it is filtered by id, and the drawer is
 * opened precisely to read the current state. Rendering the cached row would make
 * the drawer look authoritative while showing older data.
 *
 * NO PDF CONTROL, because the controller exposes no PDF route for this resource.
 *
 * ------------------------------------------------------------------
 * THE STATUS CONTROL IS OFFERED ON EVERY NON-TERMINAL RECORD, AND ONLY THERE.
 * ------------------------------------------------------------------
 * The old note on this drawer said no status control was offered because the
 * transition "could not be undone". That reasoning was sound but the conclusion has
 * changed with the phase: the transition is confirmed, the consequence is stated,
 * and PATCH /gov-fee-disbursements/{id}/status is now wired. What has NOT changed is
 * that the chain is forward-only with no reverse route — so the button appears only
 * when `nextGovFeeStatus(status)` returns a target. On a Reimbursed record, and on
 * any status a future backend version might add, it returns null and the control is
 * absent altogether, with GOV_FEE_TERMINAL_NOTE explaining why in the first case.
 *
 * The reader does not choose a status: the handler permits exactly one successor per
 * current status, so the button opens a confirmation that states the one move
 * rather than a picker in which every other option is a guaranteed 409. See
 * AdminGovFeeDisbursementStatusDialog.
 *
 * TWO TABS, ACTIVITY LAZY, ONE FOCUS TRAP, ONE SCROLL CONTAINER, NO NESTED DRAWER —
 * the same contract as every other Admin drawer. See AdminInvoiceDetailDrawer for
 * why the panel's aria-labelledby falls back to the drawer heading.
 */
const TABS = [
  { key: 'overview', label: 'Overview', icon: Landmark },
  { key: 'activity', label: 'Activity (audit log)', icon: ScrollText },
]

function AdminGovFeeDisbursementDetailDrawer({ disbursementId, onClose }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [activeTab, setActiveTab] = useState('overview')
  const tabRefs = useRef([])
  const [isStatusDialogOpen, setIsStatusDialogOpen] = useState(false)

  const { data: disbursement, isLoading, isError, error, refresh } =
    useAdminGovFeeDisbursement(disbursementId)

  // A different disbursement is a different record, so a staged status confirmation
  // from the previous one is discarded rather than left armed against it.
  useEffect(() => {
    setActiveTab('overview')
    setIsStatusDialogOpen(false)
  }, [disbursementId])

  const description = presentText(disbursement?.feeDescription)
  const reference = presentText(disbursement?.governmentReference)
  const notes = presentText(disbursement?.notes)
  const task = govFeeTaskReference(disbursement)
  const money = moneyParts(disbursement?.amount, disbursement?.currency)

  /**
   * The one legal successor for this record, or null when it is terminal. Read from
   * the same helper the status dialog uses, so the button and the confirmation can
   * never disagree about what the next step is.
   */
  const nextStatus = nextGovFeeStatus(disbursement?.status)

  const heading = disbursement
    ? `${description ?? 'Government fee'} · ${shortGuid(disbursement.id) ?? ''}`.trim()
    : 'Loading disbursement…'

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
          entityType={BILLING_AUDIT_ENTITY_TYPES['gov-fee']}
          entityId={disbursement.id}
        />
      )
    }

    return (
      <div className="flex flex-col gap-5">
        <section
          aria-labelledby="admin-gov-fee-overview-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-gov-fee-overview-heading"
            className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
          >
            <Landmark size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
            Government fee disbursement
          </h3>

          <div className="mt-3 flex flex-wrap items-center gap-2">
            <StatusPill
              label={govFeeStatusText(disbursement.status) ?? 'Unknown'}
              tone={govFeeStatusTone(disbursement.status)}
            />
          </div>

          <dl className="mt-3.5 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            <AdminDetailRow label="Fee description" value={description} />
            <AdminDetailRow label="Client company" value={disbursement.clientCompanyName} />
            <AdminDetailRow label="Government reference" value={reference} />
            {/* Nullable on the DTO: a government fee is not always tied to a renewal
                task, and that absence is reported as an absence. */}
            <AdminDetailRow label="Renewal task" value={task.present ? task.label : null} mono />
            <AdminDetailRow label="Disbursement ID" value={disbursement.id} mono />
          </dl>
        </section>

        <section
          aria-labelledby="admin-gov-fee-amount-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-gov-fee-amount-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Amount
          </h3>

          <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
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
          aria-labelledby="admin-gov-fee-timeline-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-gov-fee-timeline-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Reimbursement timeline
          </h3>

          <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            <AdminDetailRow label="Paid by firm" value={formatDateTime(disbursement.paidByFirmAt)} />
            <AdminDetailRow label="Invoiced to client" value={dateTimeText(disbursement.invoicedAt)} />
            <AdminDetailRow label="Reimbursed" value={dateTimeText(disbursement.reimbursedAt)} />
          </dl>

          {!disbursement.invoicedAt ? (
            <p className="mt-2.5 text-xs text-[#9CA3AF]">
              The client has not been invoiced for this fee yet.
            </p>
          ) : null}

          {!disbursement.reimbursedAt ? (
            <p className="mt-1.5 text-xs text-[#9CA3AF]">
              The firm has not been reimbursed for this fee yet, so the amount above
              is money still out of pocket rather than money recovered.
            </p>
          ) : null}
        </section>

        {notes && (
          <section
            aria-labelledby="admin-gov-fee-notes-heading"
            className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
          >
            <h3
              id="admin-gov-fee-notes-heading"
              className="text-sm font-semibold tracking-tight text-[#16181D]"
            >
              Notes
            </h3>
            <p className="mt-2.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
              {notes}
            </p>
          </section>
        )}

        <section
          aria-labelledby="admin-gov-fee-record-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-gov-fee-record-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Record
          </h3>

          <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
            <AdminDetailRow label="Created" value={dateTimeText(disbursement.createdAt)} />
            <AdminDetailRow label="Last updated" value={dateTimeText(disbursement.updatedAt)} />
          </dl>

          <p className="mt-3 border-t border-[#E2E4E9] pt-3 text-[11px] leading-relaxed text-[#9CA3AF]">
            No PDF is offered for a government fee: the backend exposes no document
            route for a disbursement, so there is nothing to fetch.
          </p>
        </section>

        {/*
          The heading and copy here both changed with the phase. "Read-only record"
          and READ_ONLY_NOTE claimed the record could not be changed, which is no
          longer true, and the old paragraph said a status control was deliberately
          withheld — also no longer true. What replaces them states the lifecycle and
          the irreversibility, and explains an absent control where one is genuinely
          absent: a Reimbursed disbursement is terminal, and no successor exists.
        */}
        <section
          aria-labelledby="admin-gov-fee-lifecycle-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
        >
          <h3
            id="admin-gov-fee-lifecycle-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            Record lifecycle
          </h3>

          <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
            {BILLING_LIFECYCLE_NOTE}
          </p>

          {!nextStatus && (
            <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
              {GOV_FEE_TERMINAL_NOTE}
            </p>
          )}

          <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
            {NO_MONEY_AGGREGATION_NOTE}
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
              <Landmark size={18} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <h2
                id={titleId}
                className="break-words text-base font-semibold tracking-tight text-[#16181D]"
              >
                {isLoading && !disbursement ? 'Loading disbursement…' : heading}
              </h2>
              {disbursement && (
                <div className="mt-1.5 flex flex-wrap items-center gap-x-2.5 gap-y-1.5">
                  <StatusPill
                    label={govFeeStatusText(disbursement.status) ?? 'Unknown'}
                    tone={govFeeStatusTone(disbursement.status)}
                  />
                  <span className="min-w-0 break-words font-mono text-xs text-[#6B7280]">
                    {shortGuid(disbursement.id)}
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
            aria-label="Government fee disbursement sections"
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
          {isLoading && <LoadingState label="Loading disbursement…" />}

          {isError && (
            <ErrorState
              message={extractApiErrorMessage(error, 'Could not load this government fee disbursement.')}
              onRetry={() => refresh()}
            />
          )}

          {!isLoading && !isError && disbursement && renderTab()}

          {!isLoading && !isError && !disbursement && (
            <p className="rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-2.5 text-xs text-[#92400E]">
              The request succeeded but returned no disbursement record, so there is
              nothing to display. The record may have been deleted.
            </p>
          )}
        </div>

        {/*
          THE STATUS CONTROL, rendered only when a successor exists. On a Reimbursed
          disbursement this footer is absent entirely rather than showing a disabled
          button, because "cannot advance" is a permanent property of the record and
          not a temporary unavailability.
        */}
        {!isLoading && !isError && disbursement && nextStatus && (
          <div className="flex flex-col-reverse gap-2 border-t border-[#E2E4E9] p-5 sm:flex-row sm:items-center sm:justify-between sm:p-6">
            <p className="text-xs leading-relaxed text-[#6B7280]">
              The next step is {govFeeStatusText(nextStatus) ?? nextStatus}. The
              status chain only moves forward, and the platform exposes no route that
              moves it back.
            </p>

            <button
              type="button"
              onClick={() => setIsStatusDialogOpen(true)}
              className="inline-flex shrink-0 cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2"
            >
              <ArrowRight size={15} strokeWidth={2} aria-hidden="true" />
              {BILLING_ACTION_LABELS[IRREVERSIBLE_ACTIONS.GOV_FEE_STATUS]}
            </button>
          </div>
        )}
      </aside>

      {/*
        The confirmation is a separate component so the drawer's own focus trap and
        this dialog's do not have to be coordinated here. It renders nothing at all
        when the record has no successor, so a stale `open` cannot produce a
        confirmation for a move the server would refuse.
      */}
      <AdminGovFeeDisbursementStatusDialog
        open={isStatusDialogOpen}
        disbursement={disbursement}
        onClose={() => setIsStatusDialogOpen(false)}
      />
    </div>
  )
}

export default AdminGovFeeDisbursementDetailDrawer
