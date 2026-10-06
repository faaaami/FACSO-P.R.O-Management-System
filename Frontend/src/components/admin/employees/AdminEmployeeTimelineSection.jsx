import { useState } from 'react'
import { History } from 'lucide-react'
import AdminClientSection from '../clients/AdminClientSection'
import Pagination from '../../client/billing/Pagination'
import { useAdminEmployeeTimeline } from '../../../hooks/admin/useAdminEmployees'
import { formatDateTime, presentText } from '../clients/clientDisplay'
import { timelineActionLabel, timelineEntityLabel } from './employeeDisplay'

const PAGE_SIZE = 20

/**
 * Audit trail for one employee.
 *
 * BACKING QUERY: GET /api/v1/admin/employees/{employeeId}/timeline?page&pageSize
 * Response: { employeeId, items, page, pageSize, totalCount }
 *   item: { id, action, entityType, entityId, description, createdAt }
 *
 * THIS ENDPOINT IS PAGINATED — it is the one employee child route that returns
 * real page, pageSize and totalCount — so the pager here is backed by genuine
 * metadata rather than a guess. That is the deliberate contrast with the
 * documents tab, which has no pagination at all.
 *
 * The rows come from the audit_logs table filtered on employee_id and ordered
 * created_at DESC, so the trail is newest-first and the order is fixed server
 * side; no sort control is offered because there is no sort parameter.
 *
 * `action` and `entityType` are FREE-FORM STRINGS written by command handlers —
 * "EmployeeDeleted", "Employee" and so on — not an enum. There is therefore no
 * closed set to map and no icon per event type to select. The helpers only
 * reformat what arrived and fall back to the raw value, so an event this module
 * has never seen still renders with its real name instead of a blank row or an
 * invented label.
 *
 * `description` is the human sentence the command handler wrote (for example
 * "Employee 'X' was deleted."), so it is shown when present and the row falls
 * back to the action label when it is not.
 */
function AdminEmployeeTimelineSection({ employeeId }) {
  const [page, setPage] = useState(1)

  const {
    items,
    totalCount,
    isLoading,
    isError,
    error,
    isFetching,
    refresh,
  } = useAdminEmployeeTimeline(employeeId, { page, pageSize: PAGE_SIZE })

  const toolbar =
    !isLoading && !isError && items.length > 0 ? (
      <p className="text-xs text-[#6B7280]">
        {totalCount.toLocaleString('en-US')}{' '}
        {totalCount === 1 ? 'event' : 'events'} recorded, newest first.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Timeline"
      description="Recorded changes to this employee, newest first."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading timeline…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this employee's timeline."
      isEmpty={!isLoading && !isError && items.length === 0}
      emptyMessage="No recorded events."
      emptyDescription="Changes to this employee will be listed here once they happen."
      emptyIcon={History}
    >
      <div className="flex flex-col gap-4">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing timeline…
          </p>
        )}

        <ol className="flex flex-col">
          {items.map((entry, index) => {
            const action = timelineActionLabel(entry?.action) ?? 'Recorded event'
            const entity = timelineEntityLabel(entry?.entityType)
            const description = presentText(entry?.description)

            return (
              <li
                key={entry?.id ?? `${entry?.createdAt}-${index}`}
                className="relative flex gap-3 pb-4 last:pb-0"
              >
                {/* Connector rail. aria-hidden because the ordering is already
                    conveyed by the ordered list and the timestamps. */}
                <span
                  className="relative flex w-3 shrink-0 justify-center"
                  aria-hidden="true"
                >
                  <span className="mt-1.5 h-2 w-2 rounded-full bg-[#0F9D74]" />
                  {index < items.length - 1 && (
                    <span className="absolute top-4 bottom-[-1rem] w-px bg-[#E2E4E9]" />
                  )}
                </span>

                <div className="min-w-0 flex-1">
                  <p className="break-words text-sm font-semibold text-[#16181D]">
                    {action}
                  </p>
                  {entity && (
                    <p className="mt-0.5 text-xs text-[#6B7280]">
                      Record type: {entity}
                    </p>
                  )}
                  {description && (
                    <p className="mt-1 break-words text-xs text-[#16181D]">
                      {description}
                    </p>
                  )}
                  <p className="mt-1 text-xs text-[#6B7280]">
                    <time dateTime={entry?.createdAt ?? undefined}>
                      {formatDateTime(entry?.createdAt)}
                    </time>
                  </p>
                </div>
              </li>
            )
          })}
        </ol>

        {totalCount > PAGE_SIZE && (
          <Pagination
            page={page}
            pageSize={PAGE_SIZE}
            totalCount={totalCount}
            itemLabel="event"
            itemLabelPlural="events"
            onPageChange={setPage}
          />
        )}

      </div>
    </AdminClientSection>
  )
}

export default AdminEmployeeTimelineSection
