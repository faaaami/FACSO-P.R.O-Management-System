import { Building2, CalendarClock, CheckCircle2, FileText, UserRound } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import { formatDate, shortGuid, taskStatusLabel, taskStatusTone, truncate } from './taskDisplay'

/**
 * The Admin renewal-task list table.
 *
 * BACKING QUERY: GET /api/v1/admin/tasks
 *
 * COLUMNS ARE EXACTLY THE ROW FIELDS. RenewalTaskListItemDto carries id,
 * documentId, clientCompanyId, assignedStaffId?, status, blockedReason?,
 * blockedSince?, completedAt? and createdAt — and that is the whole list. So
 * there is deliberately NO column for:
 *
 *   - a task description or title. THE LIST DTO HAS NO TEXT FIELD AT ALL beyond
 *     the status. The registry of human-readable task identity lives entirely in
 *     the detail response's `document` block (type, documentNumber), which this
 *     endpoint does not return. The "Task" column therefore shows identifiers,
 *     and long-name truncation is applied where text genuinely exists (the
 *     resolved company name) rather than to a description that was invented to
 *     fill the space.
 *   - a company NAME (the row carries `clientCompanyId` only), or an employee or
 *     legal-entity name (the DTO has neither field).
 *   - document type or number, for the same reason as above.
 *   - a step count, invoice figure or amount. None is on this DTO.
 *
 * COMPANY AND ASSIGNEE ARE RESOLVED, OR SAY SO. Both arrive as bare Guids, so
 * useAdminEntityMaps resolves them. A value that cannot be resolved — a row
 * beyond the map's 1,000-row cap, or an Agent who is not in the staff list —
 * renders as an explicitly labelled short id in muted grey, never as a blank and
 * never as a fabricated name. An UNASSIGNED task is a different thing entirely
 * and is labelled "Unassigned", because a null assignee is a real workflow state
 * rather than a lookup failure.
 *
 * STATUS IS THE ROW'S OWN VALUE, MAPPED FOR TONE ONLY. The pill shows exactly
 * what the endpoint returned, worded by the shared enum label. It is never
 * re-derived: there is no local state machine, and a status is never computed
 * from blockedSince, completedAt or createdAt, because the server owns that
 * state.
 *
 * ORDERING IS THE BACKEND'S. Rows arrive `ORDER BY created_at DESC, id ASC`.
 * The `id ASC` tie-breaker makes the order total, so paging is stable — unlike
 * the Documents registry, which sorts on a non-unique column. No column header
 * is clickable, because the endpoint exposes no sort parameter.
 *
 * NO MUTATION CONTROLS LIVE HERE. The only action is "Details", which opens the
 * drawer. Assignment is reachable solely from inside that drawer, behind a
 * confirmation; there is deliberately no inline assign, block, cancel or status
 * control in the list.
 *
 * RESPONSIVE: a real <table> from `md` up inside an overflow container, and a
 * stacked card list below it. Both are rendered from the same row data and
 * neither carries an element id, so there are no duplicate ids between the two
 * representations.
 */
function AdminTasksTable({ items, resolveClient, resolveStaff, onOpenDetails }) {
  function renderCompany(task) {
    const company = resolveClient?.(task?.clientCompanyId)

    if (!company) {
      return <span className="text-xs text-[#9CA3AF]">No company id</span>
    }

    if (company.resolved) {
      return (
        <div className="flex min-w-0 items-start gap-1.5">
          <Building2
            size={13}
            strokeWidth={1.75}
            className="mt-0.5 shrink-0 text-[#6B7280]"
            aria-hidden="true"
          />
          <p className="min-w-0 break-words text-sm text-[#16181D]" title={company.name}>
            {truncate(company.name, 34) ?? company.name}
          </p>
        </div>
      )
    }

    return (
      <div
        className="flex min-w-0 items-start gap-1.5"
        title={String(company.id)}
      >
        <Building2
          size={13}
          strokeWidth={1.75}
          className="mt-0.5 shrink-0 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <div className="min-w-0">
          <p className="min-w-0 break-words text-sm text-[#9CA3AF]">{company.fallback}</p>
          <p className="text-xs text-[#9CA3AF]">not resolvable in Admin</p>
        </div>
      </div>
    )
  }

  function renderAssignee(task) {
    // A null assignee is a genuine workflow state — a task nobody owns yet — so
    // it is stated in words rather than left blank.
    if (!task?.assignedStaffId) {
      return (
        <span className="text-xs font-medium text-[#9CA3AF]">
          Unassigned
          <span className="sr-only"> — no Agent has been assigned</span>
        </span>
      )
    }

    const assignee = resolveStaff?.(task.assignedStaffId)

    if (assignee?.resolved) {
      return (
        <div className="flex min-w-0 items-start gap-1.5">
          <UserRound
            size={13}
            strokeWidth={1.75}
            className="mt-0.5 shrink-0 text-[#6B7280]"
            aria-hidden="true"
          />
          <p className="min-w-0 break-words text-sm text-[#16181D]">
            {truncate(assignee.name, 26) ?? assignee.name}
          </p>
        </div>
      )
    }

    return (
      <div
        className="flex min-w-0 items-start gap-1.5"
        title={String(task.assignedStaffId)}
      >
        <UserRound
          size={13}
          strokeWidth={1.75}
          className="mt-0.5 shrink-0 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <div className="min-w-0">
          <p className="min-w-0 break-words text-sm text-[#9CA3AF]">
            {shortGuid(task.assignedStaffId)}
          </p>
          <p className="text-xs text-[#9CA3AF]">not resolvable in Admin</p>
        </div>
      </div>
    )
  }

  function renderStatus(task) {
    return (
      <StatusPill
        label={taskStatusLabel(task?.status) ?? 'Unknown'}
        tone={taskStatusTone(task?.status)}
      />
    )
  }

  /**
   * The secondary status facts the DTO actually carries, shown as a caption
   * under the pill instead of as extra columns. Blocked and completed are
   * mutually exclusive on the row, and each is only rendered when its own field
   * is present, so a row never claims a state the payload does not support.
   */
  function renderStatusCaption(task) {
    if (task?.blockedSince) {
      return (
        <p className="mt-1 flex items-center gap-1 text-xs text-[#B91C1C]">
          <CalendarClock size={12} strokeWidth={1.75} aria-hidden="true" />
          Blocked {formatDate(task.blockedSince)}
        </p>
      )
    }

    if (task?.completedAt) {
      return (
        <p className="mt-1 flex items-center gap-1 text-xs text-[#0B7A5A]">
          <CheckCircle2 size={12} strokeWidth={1.75} aria-hidden="true" />
          Completed {formatDate(task.completedAt)}
        </p>
      )
    }

    return null
  }

  return (
    <>
      {/* Desktop: a real table, so column headers are announced with their cells. */}
      <div className="hidden overflow-x-auto rounded-[10px] border border-[#E2E4E9] md:block">
        <table className="w-full min-w-[860px] border-collapse text-left">
          <caption className="sr-only">
            Renewal tasks, newest first. The status, company and assignee columns
            show the values the task list endpoint returned.
          </caption>
          <thead className="bg-gray-50">
            <tr>
              <th
                scope="col"
                className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Task
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Company
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Assignee
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Created
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Status
              </th>
              <th
                scope="col"
                className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((task) => {
              const taskId = shortGuid(task?.id) ?? 'Task'

              return (
                <tr
                  key={task?.id}
                  className="border-t border-[#E2E4E9] transition-colors hover:bg-gray-50/60"
                >
                  <td className="px-4 py-3.5 align-top">
                    <div className="flex min-w-0 items-start gap-2.5">
                      <FileText
                        size={15}
                        strokeWidth={1.75}
                        className="mt-0.5 shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <div className="min-w-0">
                        <p
                          className="min-w-0 break-words font-mono text-[13px] font-medium text-[#16181D]"
                          title={String(task?.id)}
                        >
                          {taskId}
                        </p>
                        <p
                          className="mt-0.5 min-w-0 break-words text-xs text-[#6B7280]"
                          title={String(task?.documentId)}
                        >
                          Document {shortGuid(task?.documentId) ?? '—'}
                        </p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3.5 align-top">{renderCompany(task)}</td>
                  <td className="px-4 py-3.5 align-top">{renderAssignee(task)}</td>
                  <td className="whitespace-nowrap px-4 py-3.5 align-top text-sm text-[#16181D]">
                    {formatDate(task?.createdAt)}
                  </td>
                  <td className="px-4 py-3.5 align-top">
                    {renderStatus(task)}
                    {renderStatusCaption(task)}
                  </td>
                  <td className="px-4 py-3.5 text-right align-top">
                    <button
                      type="button"
                      onClick={() => onOpenDetails(task.id)}
                      className="inline-flex shrink-0 cursor-pointer items-center rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                    >
                      Details
                      <span className="sr-only"> for task {taskId}</span>
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Mobile: the same rows as stacked cards. */}
      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((task) => {
          const taskId = shortGuid(task?.id) ?? 'Task'

          return (
            <li
              key={task?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-2.5">
                  <FileText
                    size={15}
                    strokeWidth={1.75}
                    className="mt-0.5 shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <div className="min-w-0">
                    <p
                      className="min-w-0 break-words font-mono text-[13px] font-medium text-[#16181D]"
                      title={String(task?.id)}
                    >
                      {taskId}
                    </p>
                    <p
                      className="mt-0.5 min-w-0 break-words text-xs text-[#6B7280]"
                      title={String(task?.documentId)}
                    >
                      Document {shortGuid(task?.documentId) ?? '—'}
                    </p>
                  </div>
                </div>
                {renderStatus(task)}
              </div>

              {renderStatusCaption(task) && (
                <div className="mt-2">{renderStatusCaption(task)}</div>
              )}

              <dl className="mt-2.5 flex flex-col gap-1.5">
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Company</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {renderCompany(task)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Assignee</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {renderAssignee(task)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Created</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {formatDate(task?.createdAt)}
                  </dd>
                </div>
              </dl>

              <button
                type="button"
                onClick={() => onOpenDetails(task.id)}
                className="mt-3 inline-flex w-full cursor-pointer items-center justify-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
              >
                Details
                <span className="sr-only"> for task {taskId}</span>
              </button>
            </li>
          )
        })}
      </ul>
    </>
  )
}

export default AdminTasksTable
