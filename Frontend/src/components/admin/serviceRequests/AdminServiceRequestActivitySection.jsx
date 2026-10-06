import { useState } from 'react'
import { ScrollText } from 'lucide-react'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { useAdminAuditLog } from '../../../hooks/admin/useAdminAuditLog'
import { formatDateTime, presentText } from './serviceRequestDisplay'

const PAGE_SIZE = 20

/**
 * The request's audit trail, read from GET /api/v1/audit-log with
 * entityType=ServiceRequest and entityId=<this request>.
 *
 * THIS IS A GLOBAL AUDIT LOG FILTERED TO ONE RECORD, NOT A REQUEST HISTORY, and
 * the distinction is stated on screen rather than left for the reader to infer.
 * The service request DTOs expose no history, no event feed and no per-step log of
 * their own; the only per-record events available anywhere are audit rows. What
 * the filter guarantees is only that EntityType == "ServiceRequest" and
 * EntityId == this id — so every row shown genuinely concerns this request, but
 * the sequence is not a complete account of it.
 *
 * It IS NOT COMPLETE FOR THREE REASONS, all of them backend behaviour rather than
 * anything this component could fix:
 *
 *   - The request's own creation and decision both write rows
 *     ("ServiceRequestCreated" by a client, and "ServiceRequestConverted" or
 *     "ServiceRequestRejected" by an admin), but nothing writes a row for reading
 *     the request, so a request nobody has opened shows one entry, not several.
 *   - An update to the request is not audited at all; there is no update command
 *     today, but nothing in the audit surface would record one if there were.
 *   - Any change made to a record this request merely points at — the employee,
 *     the entity, the document — is audited against THAT record, not this one, and
 *     so will not appear here.
 *
 * THE ACTOR MAY NOT BE A MEMBER OF THIS UI. Rows are written by whichever
 * authenticated user performed the action, and the create row is written by the
 * client who submitted the request, not by an admin. The row's userName is also
 * frequently null, because the handlers record a UserId without a name — so the
 * actor falls back to the id, and an id is never presented as a person's identity.
 *
 * Only the three actions the backend actually writes are given readable labels.
 * The action string is always shown beside the label, so an action this component
 * has never seen still renders as itself instead of as "Unknown".
 *
 * THE TAB IS MOUNTED ONLY WHEN OPEN (see AdminServiceRequestDetailDrawer), so this
 * query does not run for a drawer that is merely open on the Overview tab.
 */
function describeAction(action) {
  switch (action) {
    case 'ServiceRequestCreated':
      return 'Request submitted'
    case 'ServiceRequestConverted':
      return 'Converted to a renewal task'
    case 'ServiceRequestRejected':
      return 'Request rejected'
    default:
      return null
  }
}

function AuditRow({ entry }) {
  const label = describeAction(entry.action)
  const description = presentText(entry.description)
  const actor = presentText(entry.userName)

  return (
    <li className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-0.5">
        <p className="text-sm font-semibold text-[#16181D]">
          {label ?? 'Recorded action'}
          <span className="ml-2 font-mono text-[11px] font-normal text-[#9CA3AF]">
            {entry.action}
          </span>
        </p>
        <p className="text-xs text-[#6B7280]">
          {formatDateTime(entry.createdAt) ?? <span className="text-[#9CA3AF]">&mdash;</span>}
        </p>
      </div>

      {description ? (
        <p className="mt-1.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
          {description}
        </p>
      ) : null}

      <p className="mt-2 text-xs text-[#6B7280]">
        {actor ? (
          <span>By {actor}</span>
        ) : entry.userId ? (
          <span>
            By user{' '}
            <span className="font-mono text-[11px] text-[#9CA3AF]">{entry.userId}</span>
          </span>
        ) : (
          <span className="text-[#9CA3AF]">No user recorded</span>
        )}
      </p>
    </li>
  )
}

function AdminServiceRequestActivitySection({ requestId }) {
  const [page, setPage] = useState(1)

  const { items, totalCount, isLoading, isError, error, refresh } = useAdminAuditLog({
    entityType: 'ServiceRequest',
    entityId: requestId,
    page,
    pageSize: PAGE_SIZE,
  })

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  return (
    <div className="flex flex-col gap-4">
      <section
        aria-labelledby="admin-service-request-activity-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
      >
        <h3
          id="admin-service-request-activity-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <ScrollText size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          Activity (audit log)
        </h3>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          This is the global audit trail for this request, not a request-history
          feed. It is the platform-wide audit log filtered to records whose entity
          type is <span className="font-medium text-[#16181D]">ServiceRequest</span>{' '}
          and whose id is this request, so every entry below does concern it — but
          the log only records actions somebody took. It does not record this
          request being viewed, and it does not record the request&rsquo;s own
          timestamps.
        </p>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          Entries may come from any role. The request itself was submitted by a
          client, so the first entry here belongs to that client rather than to an
          administrator, and a later entry may belong to whoever converted or
          rejected it.
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
          message={error?.message ?? 'The audit trail for this request could not be loaded.'}
          onRetry={refresh}
        />
      ) : items.length === 0 ? (
        <EmptyState
          icon={ScrollText}
          message="No audit events returned"
          description="The audit log returned no entries for this request. It may predate audit logging, or no action has been recorded against it yet — neither means the request did not happen, only that nothing about it was written to the log."
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

export default AdminServiceRequestActivitySection
