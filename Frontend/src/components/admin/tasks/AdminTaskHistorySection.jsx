import { History } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { useAdminEntityMaps } from '../../../hooks/admin/useAdminEntityMaps'
import { useAdminTaskHistory } from '../../../hooks/admin/useAdminTasks'
import { extractApiErrorMessage } from '../../../utils/apiError'
import {
  displayText,
  formatDateTime,
  presentText,
  shortGuid,
  taskStatusLabel,
  taskStatusTone,
} from './taskDisplay'

/**
 * The event log for one task.
 *
 * BACKING QUERY: GET /api/v1/admin/tasks/{taskId}/history, fetched only when
 * this tab is first activated.
 * Returns { taskId, items, totalCount }
 *   item: RenewalTaskHistoryDto { id, status, changedBy, changedAt, note? }
 *
 * ------------------------------------------------------------------
 * THIS IS AN APPEND-ONLY EVENT LOG, NOT A STATE-TRANSITION TIMELINE.
 * ------------------------------------------------------------------
 * The obvious presentation — "Submitted -> Fee Paid -> Approved" with each status
 * appearing once, in order — would be a fabrication here, for two verified
 * reasons:
 *
 *   1. AN ASSIGNMENT EVENT REPEATS THE UNCHANGED STATUS. On assignment the
 *      handler writes `Status = task.Status`, i.e. the status the task already
 *      had, with the note "Task assigned to Agent: <name>.". So two adjacent rows
 *      can carry the same status and nothing about the task's state changed. A
 *      timeline that de-duplicated or collapsed those rows would be inventing a
 *     transition that never happened.
 *   2. THE LOG IS NOT COMPLETE. It is written by the task commands only, so it
 *      records the events the backend chose to record. Rows are never collapsed,
 *      re-ordered or merged here: each is rendered as it arrived.
 *
 * `note` is the free-text explanation the command supplied, and it is the most
 * informative field on the row ("Status changed from X to Y.", "Task assigned to
 * Agent: N."). It is shown verbatim. An absent note is a real absence and renders
 * as the dash — never as a synthesised description of what the status implies.
 *
 * `changedBy` is a bare Guid of the acting USER. Status and step commands are
 * Admin actions, so this id is normally an Admin account, which the Agent-only
 * staff map cannot resolve; it is then shown as an explicit unresolved short id
 * rather than a guessed name. Assignment is the exception, and is still recorded
 * against the ADMIN who performed it, not the Agent who received the task.
 *
 * ORDERING IS THE BACKEND'S (`changed_at ASC`) with no tie-breaker, so events
 * sharing a timestamp have no defined relative order. Rows are not re-sorted.
 *
 * The section is READ-ONLY. No control here can change a task.
 */
function AdminTaskHistorySection({ taskId }) {
  const { items, isLoading, isError, error, refresh } = useAdminTaskHistory(taskId)
  const { resolveStaff } = useAdminEntityMaps()

  function renderActor(event) {
    if (!event?.changedBy) {
      return <span className="text-[#9CA3AF]">Not recorded</span>
    }

    const actor = resolveStaff?.(event.changedBy)

    if (actor?.resolved) {
      return <span className="text-[#16181D]">{actor.name}</span>
    }

    return (
      <span className="text-[#9CA3AF]" title={String(event.changedBy)}>
        {shortGuid(event.changedBy)}
        <span className="sr-only"> — actor not resolvable to a name</span>
      </span>
    )
  }

  if (isLoading) {
    return <LoadingState label="Loading history…" />
  }

  if (isError) {
    return (
      <ErrorState
        message={extractApiErrorMessage(
          error,
          'Could not load this task’s history.',
        )}
        onRetry={() => refresh()}
      />
    )
  }

  if (items.length === 0) {
    return (
      <EmptyState
        icon={History}
        message="No history recorded yet."
        description="The backend writes a history row on every task command it executes. Nothing has been recorded against this task."
      />
    )
  }

  return (
    <div className="flex flex-col gap-3">
      <p className="text-xs text-[#6B7280]">
        {items.length.toLocaleString('en-US')}{' '}
        {items.length === 1 ? 'event' : 'events'}, oldest first. Assignment and
        status commands are recorded separately, so a status can legitimately
        appear more than once in a row: this is an event log, not a state
        timeline.
      </p>

      <ol className="flex flex-col gap-2.5">
        {items.map((event) => {
          const note = presentText(event?.note)

          return (
            <li
              key={event?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex flex-wrap items-start justify-between gap-2.5">
                <div className="min-w-0">
                  <p className="text-xs font-semibold text-[#6B7280]">
                    {formatDateTime(event?.changedAt)}
                  </p>
                  {/*
                    The status pill repeats whatever the row carries, including a
                    value identical to the previous row. That repetition is the
                    data, not a bug to be tidied away.
                  */}
                  <div className="mt-1.5">
                    <StatusPill
                      label={taskStatusLabel(event?.status) ?? 'Unknown'}
                      tone={taskStatusTone(event?.status)}
                    />
                  </div>
                </div>

                <span className="shrink-0 font-mono text-[11px] text-[#9CA3AF]">
                  {shortGuid(event?.id)}
                </span>
              </div>

              <dl className="mt-2.5 flex flex-col gap-1.5">
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Recorded by</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {renderActor(event)}
                  </dd>
                </div>
                <div className="flex flex-col gap-0.5">
                  <dt className="text-xs text-[#6B7280]">Detail</dt>
                  <dd
                    className={`min-w-0 break-words text-xs ${
                      note ? 'text-[#16181D]' : 'text-[#9CA3AF]'
                    }`}
                  >
                    {displayText(note)}
                  </dd>
                </div>
              </dl>
            </li>
          )
        })}
      </ol>
    </div>
  )
}

export default AdminTaskHistorySection
