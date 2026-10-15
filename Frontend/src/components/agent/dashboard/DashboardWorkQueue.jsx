import { Link } from 'react-router-dom'
import { RefreshCw } from 'lucide-react'
import SectionCard from '../../client/SectionCard'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import { RENEWAL_TASK_STATUS, enumLabel } from '../../client/enumLabels'
import { useAgentTasks } from '../../../hooks/agent/useAgentTasks'
import { useAgentEntityMaps } from '../../../hooks/agent/useAgentEntityMaps'
import { formatDate } from '../documents/documentDisplay'

function taskTone(status) {
  if (status === 'Blocked') return 'danger'
  if (status === 'Approved' || status === 'Updated') return 'success'
  return 'warning'
}

/**
 * "Work queue" panel: the most recent assigned renewal tasks, each openable in
 * the read-only task detail drawer.
 */
function DashboardWorkQueue({ onOpenTask }) {
  const { items, totalCount, isLoading, isError, refresh } = useAgentTasks({
    page: 1,
    pageSize: 8,
  })
  const { resolveOwner } = useAgentEntityMaps()

  let content
  if (isLoading) {
    content = <LoadingState label="Loading renewal tasks…" />
  } else if (isError) {
    content = <ErrorState message="Could not load your renewal tasks." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={RefreshCw}
        message="No renewal tasks assigned."
        description="Tasks will appear here as client documents enter the renewal workflow."
      />
    )
  } else {
    content = (
      <>
        <ul className="divide-y divide-[#E2E4E9]">
          {items.map((task) => {
            const owner = resolveOwner(task)
            return (
              <li key={task.id} className="flex items-start justify-between gap-3 py-2.5">
                <div className="min-w-0">
                  <p className="flex items-center gap-2 text-sm font-semibold text-[#16181D]">
                    {enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'Renewal Task'}
                    <StatusPill
                      label={enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'Pending'}
                      tone={taskTone(task.status)}
                    />
                  </p>
                  <p className="mt-0.5 truncate text-xs text-[#6B7280]">
                    {owner ? (owner.name ?? owner.fallback) : 'Unassigned owner'}
                    {task.documentId ? ` · Doc ${String(task.documentId).slice(0, 8)}` : ''}
                  </p>
                  {task.createdAt && (
                    <p className="mt-0.5 text-xs text-[#9CA3AF]">
                      Started {formatDate(task.createdAt)}
                    </p>
                  )}
                </div>
                <button
                  type="button"
                  onClick={() => onOpenTask(task.id)}
                  className="shrink-0 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
                >
                  Open
                  <span className="sr-only">
                    {' '}
                    renewal task {enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'task'}
                  </span>
                </button>
              </li>
            )
          })}
        </ul>
        <div className="mt-3 flex items-center justify-between gap-2">
          <p className="text-xs text-[#9CA3AF]">
            Showing {items.length} of {totalCount} task{totalCount === 1 ? '' : 's'}
          </p>
          <Link
            to="/renewal-tasks"
            className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          >
            View all tasks
          </Link>
        </div>
      </>
    )
  }

  return (
    <SectionCard
      title="Work Queue"
      icon={RefreshCw}
      subtitle="Your assigned renewal tasks"
      badge={
        <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280]">
          {totalCount}
        </span>
      }
    >
      {content}
    </SectionCard>
  )
}

export default DashboardWorkQueue
