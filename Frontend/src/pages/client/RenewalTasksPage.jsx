import { useState } from 'react'
import { Calendar, RefreshCw } from 'lucide-react'
import { format } from 'date-fns'
import RenewalTaskSummaryCards from '../../components/client/renewalTasks/RenewalTaskSummaryCards'
import RenewalTaskFilters from '../../components/client/renewalTasks/RenewalTaskFilters'
import RenewalTaskTable from '../../components/client/renewalTasks/RenewalTaskTable'
import RenewalTaskDetailDrawer from '../../components/client/renewalTasks/RenewalTaskDetailDrawer'
import DocumentDetailDrawer from '../../components/client/documents/DocumentDetailDrawer'
import { useRenewalTasks } from '../../hooks/client/useRenewalTasks'

const PAGE_SIZE = 10

function buildParams(filters, page) {
  const params = { page, pageSize: PAGE_SIZE }
  if (filters.status) params.status = filters.status
  return params
}

function hasActiveFilters(filters) {
  return Boolean(filters.status)
}

function RenewalTasksPage() {
  const [filters, setFilters] = useState({})
  const [page, setPage] = useState(1)
  const [selectedTaskId, setSelectedTaskId] = useState(null)
  const [selectedDocumentId, setSelectedDocumentId] = useState(null)

  const { data: totalData } = useRenewalTasks({ page: 1, pageSize: 1 })
  const { data: awaitingData } = useRenewalTasks({ page: 1, pageSize: 1, status: 'AwaitingApproval' })
  const { data: blockedData } = useRenewalTasks({ page: 1, pageSize: 1, status: 'Blocked' })
  const { data: approvedData } = useRenewalTasks({ page: 1, pageSize: 1, status: 'Approved' })
  const {
    data: tasks,
    isLoading: tasksLoading,
    isError: tasksError,
    refetch: tasksRefetch,
  } = useRenewalTasks(buildParams(filters, page))

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  const updateFilters = (next) => {
    setFilters(next)
    setPage(1)
  }

  const clearFilters = () => {
    setFilters({})
    setPage(1)
  }

  const openTask = (taskId) => setSelectedTaskId(taskId)
  const closeTask = () => setSelectedTaskId(null)

  const openDocument = (documentId) => setSelectedDocumentId(documentId)
  const closeDocument = () => setSelectedDocumentId(null)

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Renewal Tasks</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <RefreshCw size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Track documents in the renewal workflow and see where the PRO team is in each task.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {/* Summary cards */}
      <RenewalTaskSummaryCards
        total={totalData?.totalCount ?? 0}
        awaitingApproval={awaitingData?.totalCount ?? 0}
        blocked={blockedData?.totalCount ?? 0}
        approved={approvedData?.totalCount ?? 0}
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
        onSelect={openTask}
      />

      {/* Detail drawer */}
      {selectedTaskId && (
        <RenewalTaskDetailDrawer
          taskId={selectedTaskId}
          onClose={closeTask}
          onOpenDocument={openDocument}
        />
      )}

      {/* Document detail drawer */}
      {selectedDocumentId && (
        <DocumentDetailDrawer
          documentId={selectedDocumentId}
          onClose={closeDocument}
        />
      )}
    </div>
  )
}

export default RenewalTasksPage