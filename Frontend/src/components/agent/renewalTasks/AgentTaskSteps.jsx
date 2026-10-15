import { format } from 'date-fns'
import { ListChecks } from 'lucide-react'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import EmptyState from '../../client/EmptyState'
import { useAgentTaskSteps } from '../../../hooks/agent/useAgentTaskSteps'

function formatDateTime(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy, HH:mm')
}

function AgentTaskSteps({ taskId }) {
  const { data, isLoading, isError, refetch } = useAgentTaskSteps(taskId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading renewal steps…" />
  } else if (isError) {
    content = <ErrorState message="Could not load renewal steps." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={ListChecks}
        message="No steps recorded yet."
        description="Completed renewal steps will appear here."
      />
    )
  } else {
    content = (
      <ol className="space-y-2.5">
        {data.items.map((step) => (
          <li key={step.id} className="rounded-[8px] border border-[#E2E4E9] bg-white p-2.5">
            <div className="flex flex-wrap items-center gap-2">
              <p className="text-sm font-semibold text-[#16181D]">{step.stepName}</p>
              <span className="text-xs text-[#9CA3AF]">{formatDateTime(step.completedAt) ?? 'N/A'}</span>
            </div>
            {step.referenceNumber && (
              <p className="mt-1 text-xs text-[#6B7280]">
                Reference: <span className="font-mono">{step.referenceNumber}</span>
              </p>
            )}
            {step.proofFileUrl && (
              <p className="mt-1 text-xs text-[#6B7280] break-all">Proof: {step.proofFileUrl}</p>
            )}
            <p className="mt-1 text-[11px] font-mono text-[#9CA3AF] break-all">
              Completed by: {step.completedBy}
            </p>
          </li>
        ))}
      </ol>
    )
  }

  return (
    <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
        Renewal Steps ({data?.totalCount ?? 0})
      </span>
      <div className="mt-3">{content}</div>
    </div>
  )
}

export default AgentTaskSteps
