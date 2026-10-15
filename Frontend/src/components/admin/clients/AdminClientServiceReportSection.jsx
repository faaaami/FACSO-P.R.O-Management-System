import { useState } from 'react'
import { ClipboardList } from 'lucide-react'
import AdminClientSection, {
  AdminClientRecordCard,
  AdminClientRecordList,
} from './AdminClientSection'
import StatusPill from '../../client/StatusPill'
import { useAdminClientServiceReport } from '../../../hooks/admin/useAdminClients'
import {
  MISSING_VALUE,
  displayText,
  formatDate,
  presentText,
} from './clientDisplay'
import { taskStatusLabel, taskStatusTone } from '../tasks/taskDisplay'

/** Shown in place of a count the request has not supplied yet. */
function countOrDash(value) {
  return Number.isFinite(value) ? value.toLocaleString('en-US') : displayText(null)
}

/**
 * Service report for one client company — counts and tasks only.
 *
 * BACKING QUERY: GET /api/v1/admin/clients/{id}/service-report, with no date
 * range sent. The endpoint accepts from/to and the repository ignores both, so
 * the section is all-time and says so. A date control is deliberately absent: it
 * would be a filter that cannot filter.
 *
 * NO MONEY APPEARS HERE. The invoice rows carry an `amount` with no currency
 * field, and the summary's totalInvoiced and totalPaid are summed across
 * retainer and service-fee invoices with no currency grouping. Rendering either
 * with a currency symbol — the shared formatters default to AED — would assert a
 * single-currency figure the data cannot support. The hook does not return them
 * at all, so the omission is structural rather than a styling choice. There is no
 * PDF download either, for the same reason: the same repository backs it.
 *
 * THE TWO HEADLINE COUNTS ARE NOT CALLED "COMPLETED" AND "PENDING", because they
 * are not that. completedTasks counts only tasks whose status is exactly
 * "Updated", and pendingTasks is simply every remaining row — so an Approved
 * task and a Blocked task are BOTH counted as pending. This section therefore
 * labels them "Updated" and "Not yet updated", and the note beneath the counts
 * spells out which statuses fall in each. The invoice count is labelled "Invoices
 * on file" and is an all-time count, since the endpoint has no status filter.
 *
 * `task.status` values are the RenewalTaskStatus names produced by the SQL, so
 * taskStatusLabel and taskStatusTone apply unchanged. A row with no document
 * number is normal — the task's document is joined with a LEFT JOIN — and is
 * shown as the honest dash. Assigned staff is likewise nullable, and it is the
 * one field a task assignment changes; useAdminTaskActions invalidates
 * ['admin','client'] so this list does not show a previous assignee.
 *
 * The counts render on every successful response, including when there are no
 * tasks, because "Invoices on file" is independent of the task list. There is no
 * section-level empty state for that reason.
 *
 * ONE LIMITATION, NOT FIXABLE HERE: GetServiceReport declares no 404 and never
 * checks that the company still exists, so a missing or soft-deleted company
 * returns 200 with empty lists and a zeroed summary and renders as "No tasks
 * recorded." This client cannot distinguish that from a real zero. The fix is a
 * backend existence check, not a frontend one; until then the drawer's own
 * header query is what reveals a missing company.
 */
function AdminClientServiceReportSection({ clientId }) {
  const [showAllTasks, setShowAllTasks] = useState(false)

  const {
    tasks,
    totalTasks,
    updatedTasks,
    notUpdatedTasks,
    totalInvoices,
    isLoading,
    isError,
    error,
    isFetching,
    refresh,
  } = useAdminClientServiceReport(clientId)

  // A long-lived company accumulates a long task list. The endpoint is
  // unpaginated, so the choice is to render all of it or to cap the initial
  // render and offer an explicit expansion — the user asks for more rather than
  // the view silently deciding what is worth showing.
  const CAP = 10
  const visibleTasks = showAllTasks ? tasks : tasks.slice(0, CAP)
  const hiddenCount = tasks.length - visibleTasks.length

  const toolbar =
    !isLoading && !isError ? (
      <p className="text-xs text-[#6B7280]">
        All-time totals — the endpoint's date parameters are accepted but not
        applied, so this report cannot be narrowed to a period.
      </p>
    ) : null

  return (
    <AdminClientSection
      title="Service report"
      description="Task history and invoice count for this company."
      toolbar={toolbar}
      loading={isLoading}
      loadingLabel="Loading service report…"
      error={isError ? error : null}
      onRetry={() => refresh()}
      errorMessage="Could not load this company's service report."
      isEmpty={false}
    >
      <div className="flex flex-col gap-4">
        {isFetching && !isLoading && (
          <p className="text-xs text-[#6B7280]" role="status">
            Refreshing service report…
          </p>
        )}

        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          {[
            { label: 'Tasks', value: countOrDash(totalTasks) },
            { label: 'Updated', value: countOrDash(updatedTasks) },
            { label: 'Not yet updated', value: countOrDash(notUpdatedTasks) },
            { label: 'Invoices on file', value: countOrDash(totalInvoices) },
          ].map((entry) => (
            <div
              key={entry.label}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-3 py-2.5"
            >
              <dt className="text-xs text-[#6B7280]">{entry.label}</dt>
              <dd className="mt-0.5 text-lg font-semibold tabular-nums text-[#16181D]">
                {entry.value}
              </dd>
            </div>
          ))}
        </dl>

        {/*
          The count provenance note. Without it "Not yet updated" reads as
          work-in-progress and understates Blocked tasks, which cannot progress
          on their own — an unblock by an Admin is the only way out of that
          status. Note that unblocking MOVES a task between the two states this
          note groups, and it does so without changing any total, because both
          `Blocked` and `Submitted` fall under "Not yet updated". Only an
          unblock followed by completion to `Updated` moves anything across.
        */}
        <p className="text-xs text-[#6B7280]">
          &ldquo;Updated&rdquo; counts only tasks whose status is exactly Updated.
          &ldquo;Not yet updated&rdquo; is every other task, which includes Approved
          and Blocked as well as work genuinely still in progress. Amounts are not
          shown: the report returns invoice amounts without a currency, and its
          totals add retainer and service-fee invoices together.
        </p>

        <div>
          <h4 className="mb-2 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
            Tasks
          </h4>

          {tasks.length === 0 ? (
            <p className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-3 py-4 text-center text-xs text-[#6B7280]">
              <ClipboardList className="mx-auto mb-2 h-5 w-5" aria-hidden="true" />
              No tasks recorded. The counts above still cover this company&apos;s
              invoices.
            </p>
          ) : (
            <>
              <AdminClientRecordList items={visibleTasks}>
                {(task) => (
                  <AdminClientRecordCard
                    key={task?.id}
                    title={presentText(task?.documentNumber) ?? MISSING_VALUE}
                    subtitle={presentText(task?.assignedStaffName) ?? 'Unassigned'}
                    trailing={
                      <StatusPill
                        label={taskStatusLabel(task?.status) ?? 'Unknown'}
                        tone={taskStatusTone(task?.status)}
                      />
                    }
                    meta={[
                      { label: 'Completed', value: formatDate(task?.completedAt) },
                    ]}
                  />
                )}
              </AdminClientRecordList>

              {hiddenCount > 0 && (
                <button
                  type="button"
                  onClick={() => setShowAllTasks(true)}
                  className="mt-2.5 w-full cursor-pointer rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                >
                  {`Show all ${tasks.length} tasks`}
                  <span className="sr-only"> in this service report</span>
                </button>
              )}
            </>
          )}
        </div>
      </div>
    </AdminClientSection>
  )
}

export default AdminClientServiceReportSection
