import { format } from 'date-fns'
import { History } from 'lucide-react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import StatusPill from '../../client/StatusPill'
import { useAgentTaskHistory } from '../../../hooks/agent/useAgentTaskHistory'
import { RENEWAL_TASK_STATUS, enumLabel } from '../../client/enumLabels'

function formatDateTime(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy, HH:mm')
}

function taskStatusTone(status) {
  if (status === 'Blocked') return 'danger'
  if (status === 'Approved' || status === 'Updated') return 'success'
  return 'warning'
}

function AgentTaskHistory({ taskId }) {
  const { data, isLoading, isError, refetch } = useAgentTaskHistory(taskId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading task history…" />
  } else if (isError) {
    content = <ErrorState message="Could not load task history." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={History}
        message="No status changes yet."
        description="Status updates will appear here as this task moves through renewal."
      />
    )
  } else {
    content = (
      <ol className="relative space-y-4 before:absolute before:left-1.5 before:top-1 before:bottom-1 before:w-px before:bg-[#E2E4E9]">
        {data.items.map((item) => (
          <li key={item.id} className="relative pl-8">
            <span
              className={`absolute left-0 top-1 h-3 w-3 rounded-full border-2 border-white shadow-[0_0_0_1px_#E2E4E9] ${
                item.status === 'Blocked' ? 'bg-[#DC2626]' : 'bg-[#0F9D74]'
              }`}
              aria-hidden="true"
            />
            <div className="flex flex-wrap items-center gap-2">
              <StatusPill
                label={enumLabel(RENEWAL_TASK_STATUS, item.status) ?? 'Pending'}
                tone={taskStatusTone(item.status)}
              />
              <span className="text-xs text-[#9CA3AF]">{formatDateTime(item.changedAt) ?? 'N/A'}</span>
            </div>
            {item.note && <p className="mt-1.5 text-sm text-[#16181D] break-words">{item.note}</p>}
            <p className="mt-1 text-[11px] font-mono text-[#9CA3AF] break-all">
              Changed by: {item.changedBy}
            </p>
          </li>
        ))}
      </ol>
    )
  }

  return (
    <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
        History ({data?.totalCount ?? 0})
      </span>
      <div className="mt-3">{content}</div>
    </div>
  )
}

export default AgentTaskHistory
