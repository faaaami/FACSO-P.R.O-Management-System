import { AlertTriangle, OctagonAlert } from 'lucide-react'
import SectionCard from '../../client/SectionCard'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { useAgentTasks } from '../../../hooks/agent/useAgentTasks'
import { useAgentEntityMaps } from '../../../hooks/agent/useAgentEntityMaps'

/**
 * "Attention needed" panel: renewal tasks that are currently Blocked, with
 * the blocking reason. Backed by GET /api/v1/agent/tasks?status=Blocked.
 */
function DashboardAttentionNeeded({ onOpenTask }) {
  const { items, isLoading, isError, refresh } = useAgentTasks({
    page: 1,
    pageSize: 5,
    status: 'Blocked',
  })
  const { resolveOwner } = useAgentEntityMaps()

  let content
  if (isLoading) {
    content = <LoadingState label="Loading blocked tasks…" />
  } else if (isError) {
    content = <ErrorState message="Could not load blocked tasks." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={OctagonAlert}
        message="Nothing blocked."
        description="You have no renewal tasks waiting on action."
      />
    )
  } else {
    content = (
      <ul className="divide-y divide-[#E2E4E9]">
        {items.map((task) => {
          const owner = resolveOwner(task)
          return (
            <li key={task.id} className="flex items-start justify-between gap-3 py-2.5">
              <div className="min-w-0">
                <p className="flex items-center gap-1.5 text-sm font-semibold text-[#16181D]">
                  <AlertTriangle size={13} strokeWidth={2} className="shrink-0 text-[#DC2626]" aria-hidden="true" />
                  Task #{String(task.id).slice(0, 8)}
                </p>
                <p className="mt-0.5 truncate text-xs text-[#6B7280]">
                  {owner ? (owner.name ?? owner.fallback) : 'Unassigned owner'}
                </p>
                {task.blockedReason && (
                  <p className="mt-1 line-clamp-2 text-xs font-medium text-[#DC2626]">
                    {task.blockedReason}
                  </p>
                )}
              </div>
              <button
                type="button"
                onClick={() => onOpenTask(task.id)}
                className="shrink-0 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
              >
                Review
                <span className="sr-only"> blocked task {String(task.id).slice(0, 8)}</span>
              </button>
            </li>
          )
        })}
      </ul>
    )
  }

  return (
    <SectionCard
      title="Attention Needed"
      icon={AlertTriangle}
      subtitle="Blocked renewal tasks waiting on you"
    >
      {content}
    </SectionCard>
  )
}

export default DashboardAttentionNeeded
