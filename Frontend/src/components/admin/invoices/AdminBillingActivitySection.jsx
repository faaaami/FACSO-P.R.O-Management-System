import { useState } from 'react'
import { ScrollText } from 'lucide-react'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { useAdminAuditLog } from '../../../hooks/admin/useAdminAuditLog'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { dateTimeText, presentText, shortGuid } from './billingDisplay'

/**
 * The audit trail for one billing record, read from GET /api/v1/audit-log with an
 * exact `entityType` + `entityId` pair.
 *
 * ------------------------------------------------------------------
 * THE ACTION STRINGS BELOW WERE READ OUT OF THE HANDLERS, NOT GUESSED.
 * ------------------------------------------------------------------
 * Every entry is the `Action` literal of a real `AuditLog` write, and only those
 * exact strings are labelled:
 *
 *   RetainerInvoice      RetainerInvoiceCreated              CreateRetainerInvoiceCommandHandler
 *                        RetainerInvoicePaid                 MarkRetainerInvoicePaidCommandHandler
 *                        RetainerInvoiceVoided                VoidRetainerInvoiceCommandHandler
 *   ServiceFeeInvoice    ServiceFeeInvoiceCreated            ServiceFeeInvoiceCreationService
 *                        ServiceFeeInvoicePaid               MarkServiceFeeInvoicePaidCommandHandler
 *                        ServiceFeeInvoiceVoided              VoidServiceFeeInvoiceCommandHandler
 *   GovFeeDisbursement   GovFeeDisbursementCreated           CreateGovFeeDisbursementCommandHandler
 *                        GovFeeDisbursementStatusUpdated     UpdateGovFeeDisbursementStatusCommandHandler
 *   PaymentOrder         PaymentOrderCreated                 CreatePaymentOrderCommandHandler
 *                        PaymentOrderRetried                 CreatePaymentOrderCommandHandler (duplicate path)
 *                        PaymentOrderCancelled               CancelPaymentOrderCommandHandler
 *                        PaymentCompletedFromWebhook         CompletePaymentFromWebhookCommandHandler
 *
 * Note that the two "marked as paid" actions are `…InvoicePaid`, not
 * `…InvoiceMarkedPaid`, and that the completion action is named after the *payment*
 * rather than the order. An action this map has never seen still renders as itself
 * with a neutral "Recorded action" heading, so a future backend action shows up
 * rather than being swallowed.
 *
 * THE RAW STRING IS ALWAYS SHOWN BESIDE THE LABEL. The friendly wording is a
 * convenience; the raw action is the part an administrator can match against the
 * audit table itself, so it is never hidden.
 *
 * ------------------------------------------------------------------
 * THIS IS A FILTERED GLOBAL LOG, NOT A RECORD HISTORY.
 * ------------------------------------------------------------------
 * The filter guarantees only that EntityType and EntityId match, so every row shown
 * does concern this record. It does NOT guarantee completeness: the log records
 * actions somebody took, so reading a record writes nothing, and a record nobody has
 * acted on shows a single creation row rather than many. A payment's Razorpay
 * settlement, for instance, is recorded against the payment order by the webhook and
 * appears here as whatever that handler wrote.
 *
 * THE ACTOR MAY NOT BE A MEMBER OF THIS UI, and their name is frequently ABSENT.
 * Several handlers record a UserId without a UserName, so `userName` is often null.
 * The row then falls back to the id, rendered as an id — it is never presented as a
 * person's name, because an id is not a name.
 *
 * THE TAB IS MOUNTED ONLY WHEN OPEN (see the three drawers), so a drawer sitting on
 * its Overview tab issues no audit request at all.
 */
const PAGE_SIZE = 20

/**
 * Readable wording per verified action string, keyed by the raw string itself so
 * there is no separate vocabulary to drift from the log.
 */
const ACTION_LABELS = {
  RetainerInvoiceCreated: 'Invoice created',
  RetainerInvoicePaid: 'Marked as paid',
  RetainerInvoiceVoided: 'Voided',
  ServiceFeeInvoiceCreated: 'Invoice created',
  ServiceFeeInvoicePaid: 'Marked as paid',
  ServiceFeeInvoiceVoided: 'Voided',
  GovFeeDisbursementCreated: 'Disbursement created',
  GovFeeDisbursementStatusUpdated: 'Status changed',
  PaymentOrderCreated: 'Payment order created',
  PaymentOrderRetried: 'Payment order retried',
  PaymentOrderCancelled: 'Payment order cancelled',
  PaymentCompletedFromWebhook: 'Payment completed (gateway webhook)',
}

/** A wording for the entity, used only to describe the log in prose. */
const ENTITY_PROSE = {
  RetainerInvoice: 'retainer invoice',
  ServiceFeeInvoice: 'Service Fee invoice',
  GovFeeDisbursement: 'government fee disbursement',
  PaymentOrder: 'payment order',
}

function AuditRow({ entry }) {
  const label = ACTION_LABELS[entry.action] ?? null
  const description = presentText(entry.description)
  const actorName = presentText(entry.userName)

  return (
    <li className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
        <p className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
          {label ?? 'Recorded action'}
          <span className="ml-2 font-mono text-[11px] font-normal text-[#9CA3AF]">
            {entry.action}
          </span>
        </p>
        <p className="shrink-0 text-xs text-[#6B7280]">{dateTimeText(entry.createdAt)}</p>
      </div>

      {description ? (
        <p className="mt-1.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
          {description}
        </p>
      ) : null}

      <p className="mt-2 text-xs text-[#6B7280]">
        {actorName ? (
          <span>By {actorName}</span>
        ) : entry.userId ? (
          // An id is rendered as an id, with its short form in the text and the full
          // value in the title. It is never labelled as a person's name.
          <span title={String(entry.userId)}>
            By user{' '}
            <span className="font-mono text-[11px] text-[#9CA3AF]">
              {shortGuid(entry.userId)}
            </span>
          </span>
        ) : (
          <span className="text-[#9CA3AF]">No user recorded</span>
        )}
      </p>
    </li>
  )
}

function AdminBillingActivitySection({ entityType, entityId }) {
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isError, error, refresh } = useAdminAuditLog({
    entityType,
    entityId,
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))
  const prose = ENTITY_PROSE[entityType] ?? 'record'

  return (
    <div className="flex flex-col gap-4">
      <section
        aria-labelledby="admin-billing-activity-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
      >
        <h3
          id="admin-billing-activity-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <ScrollText size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          Activity (audit log)
        </h3>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          This is the global audit trail for this {prose}, not a history feed. It is
          the platform-wide audit log filtered to records whose entity type is{' '}
          <span className="font-medium text-[#16181D]">{entityType}</span> and whose
          id is this record, so every entry below does concern it &mdash; but the log
          only records actions somebody took. It does not record this record being
          viewed, and it does not record the record&rsquo;s own timestamps.
        </p>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          Entries may have been written by any role, and several handlers record the
          acting user as an id with no name attached. Where a name is missing the row
          shows the id instead; an id is not a person&rsquo;s name and is never
          presented as one.
        </p>
      </section>

      {isLoading ? (
        <div className="flex flex-col gap-2.5" aria-hidden="true">
          {[0, 1, 2].map((row) => (
            <div
              key={row}
              className="animate-pulse rounded-[12px] border border-[#E2E4E9] bg-white p-3.5"
            >
              <div className="w-2/5 rounded-[6px] bg-[#F7F8FA]" style={{ height: 14 }} />
              <div className="mt-2.5 w-4/5 rounded-[6px] bg-[#F7F8FA]" style={{ height: 12 }} />
            </div>
          ))}
        </div>
      ) : isError ? (
        <ErrorState
          message={extractApiErrorMessage(error, `The audit trail for this ${prose} could not be loaded.`)}
          onRetry={refresh}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={ScrollText}
          message="No audit events returned"
          description={`The audit log returned no entries for this ${prose}. It may predate audit logging, or no action has been recorded against it yet — neither means the ${prose} did not happen, only that nothing about it was written to the log.`}
        />
      ) : (
        <>
          <p className="text-xs text-[#9CA3AF]">
            {totalCount.toLocaleString('en-US')}{' '}
            {totalCount === 1 ? 'entry' : 'entries'} recorded
          </p>

          <ul className="flex flex-col gap-2.5">
            {items.map((entry) => (
              <AuditRow key={entry.id} entry={entry} />
            ))}
          </ul>

          {totalPages > 1 ? (
            <nav
              aria-label="Audit trail pages"
              className="flex items-center justify-between gap-3 border-t border-[#E2E4E9] pt-3"
            >
              <button
                type="button"
                onClick={() => setPage((current) => Math.max(1, current - 1))}
                disabled={page <= 1}
                className="cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-40"
              >
                Previous
              </button>

              <p className="text-xs text-[#6B7280]">
                Page {page} of {totalPages}
              </p>

              <button
                type="button"
                onClick={() => setPage((current) => current + 1)}
                disabled={page >= totalPages}
                className="cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:cursor-not-allowed disabled:opacity-40"
              >
                Next
              </button>
            </nav>
          ) : null}
        </>
      )}
    </div>
  )
}

export default AdminBillingActivitySection
