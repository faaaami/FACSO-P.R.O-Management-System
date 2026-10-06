import { useState } from 'react'
import { Calendar, RefreshCw } from 'lucide-react'
import { format } from 'date-fns'
import RenewalTaskSummaryCards from '../../components/client/renewalTasks/RenewalTaskSummaryCards'
import RenewalTaskFilters from '../../components/client/renewalTasks/RenewalTaskFilters'
import RenewalTaskTable from '../../components/client/renewalTasks/RenewalTaskTable'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentTaskDetailDrawer from '../../components/agent/renewalTasks/AgentTaskDetailDrawer'
import { useAgentTasks } from '../../hooks/agent/useAgentTasks'
import { useAgentWorkload } from '../../hooks/agent/useAgentWorkload'

const PAGE_SIZE = 10

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

const TASK_STATUS_KEYS = [
  'Submitted',
  'FeePaid',
  'AwaitingApproval',
  'Blocked',
  'Approved',
  'Updated',
]

function buildParams(filters, page) {
  const params = { page, pageSize: PAGE_SIZE }
  if (filters.status) params.status = filters.status
  return params
}

function hasActiveFilters(filters) {
  return Boolean(filters.status)
}

function AgentRenewalTasksPage() {
  const [filters, setFilters] = useState({})
  const [page, setPage] = useState(1)
  const [selectedTaskId, setSelectedTaskId] = useState(null)

  const {
    data: tasks,
    isLoading: tasksLoading,
    isError: tasksError,
    refresh: tasksRefetch,
  } = useAgentTasks(buildParams(filters, page))

  const { data: workload } = useAgentWorkload()

  const tasksByStatus = workload?.tasksByStatus ?? {}
  const totalTaskCount = TASK_STATUS_KEYS.reduce(
    (sum, status) => sum + (tasksByStatus[status] ?? 0),
    0
  )

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  const updateFilters = (next) => {
    setFilters(next)
    setPage(1)
  }

  const clearFilters = () => {
    setFilters({})
    setPage(1)
  }

  return (
    <div className="space-y-6">
      <AgentPageHeader
        title="Renewal Tasks"
        badge={PORTAL_BADGE}
        description="Renewal tasks assigned to you. Track status, blocked items and the processing trail per task."
        meta={
          <div className="flex items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        }
      />

      <RenewalTaskSummaryCards
        total={totalTaskCount}
        awaitingApproval={tasksByStatus.AwaitingApproval ?? 0}
        blocked={tasksByStatus.Blocked ?? 0}
        approved={tasksByStatus.Approved ?? 0}
      />

      <RenewalTaskFilters
        filters={filters}
        onChange={updateFilters}
        onClear={clearFilters}
        isDirty={hasActiveFilters(filters)}
      />

      <RenewalTaskTable
        data={tasks}
        isLoading={tasksLoading}
        isError={tasksError}
        onRetry={() => tasksRefetch()}
        page={page}
        pageSize={PAGE_SIZE}
        onPageChange={setPage}
        onSelect={setSelectedTaskId}
      />

      {selectedTaskId && (
        <AgentTaskDetailDrawer taskId={selectedTaskId} onClose={() => setSelectedTaskId(null)} />
      )}
    </div>
  )
}

export default AgentRenewalTasksPage
