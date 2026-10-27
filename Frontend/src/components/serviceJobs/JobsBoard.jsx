import { useEffect, useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import { AlertTriangle, Briefcase, Eye, RefreshCw, Search } from 'lucide-react'
import SectionCard from '../client/SectionCard'
import StatusPill from '../client/StatusPill'
import LoadingState from '../client/LoadingState'
import ErrorState from '../client/ErrorState'
import EmptyState from '../client/EmptyState'
import Pagination from '../client/billing/Pagination'
import { extractApiErrorMessage } from '../../utils/apiError'
import { getJobSummary, listJobs } from '../../api/serviceJobs'
import JobDetailDrawer from './JobDetailDrawer'
import { CATEGORIES, CATEGORY_LABELS, JOB_STATUS, formatAed, formatDate } from './labels'
import { Button, Chip, Input, KpiTile, Select } from './ui'

const PAGE_SIZE = 20

const QUICK_FILTERS = [
  { key: 'open', label: 'Open', params: { openOnly: true } },
  { key: 'new', label: 'New', params: { status: 'New' } },
  { key: 'progress', label: 'In progress', params: { status: 'InProgress' } },
  { key: 'hold', label: 'On hold', params: { status: 'OnHold' } },
  { key: 'overdue', label: 'Overdue', params: { overdueOnly: true } },
  { key: 'unassigned', label: 'Unassigned', params: { unassignedOnly: true }, adminOnly: true },
  { key: 'completed', label: 'Completed', params: { status: 'Completed' } },
  { key: 'all', label: 'All', params: {} },
]

/**
 * Jobs list with KPI tiles, quick filters, search and the detail drawer.
 * The API scopes rows to the caller's role.
 */
function JobsBoard({ role, extraParams = {}, title = 'Jobs', headerAction, openJobId, onOpenJobHandled }) {
  const [filter, setFilter] = useState('open')
  const [category, setCategory] = useState('')
  const [search, setSearch] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selectedId, setSelectedId] = useState(null)

  useEffect(() => {
    const timer = setTimeout(() => { setDebouncedSearch(search.trim()); setPage(1) }, 300)
    return () => clearTimeout(timer)
  }, [search])

  useEffect(() => {
    if (openJobId) {
      setSelectedId(openJobId)
      onOpenJobHandled?.()
    }
  }, [openJobId, onOpenJobHandled])

  const quick = QUICK_FILTERS.find((f) => f.key === filter) ?? QUICK_FILTERS[0]
  const params = {
    ...quick.params,
    ...extraParams,
    category: category || undefined,
    search: debouncedSearch || undefined,
    page,
    pageSize: PAGE_SIZE,
  }

  const listQuery = useQuery({
    queryKey: ['service-jobs', role, 'list', params],
    queryFn: () => listJobs(role, params),
    placeholderData: (previous) => previous,
  })

  const summaryQuery = useQuery({
    queryKey: ['service-jobs', role, 'summary'],
    queryFn: () => getJobSummary(role),
  })

  const summary = summaryQuery.data
  const items = listQuery.data?.items ?? []
  const total = listQuery.data?.totalCount ?? 0
  const isStaff = role !== 'Client'

  const select = (key) => { setFilter(key); setPage(1) }

  let content
  if (listQuery.isLoading) {
    content = <LoadingState label="Loading jobs…" />
  } else if (listQuery.isError) {
    content = <ErrorState message={extractApiErrorMessage(listQuery.error, 'Could not load jobs.')} onRetry={() => listQuery.refetch()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState icon={Briefcase} message="No jobs match."
        description={filter === 'all' && !debouncedSearch && !category ? 'Jobs appear here once they are created.' : 'Try another filter or search.'} />
    )
  } else {
    content = (
      <>
        <div className="hidden overflow-x-auto md:block">
          <table className="w-full min-w-[980px] text-sm">
            <thead>
              <tr className="border-b border-[#E2E4E9] text-left text-xs font-semibold uppercase tracking-wide text-[#6B7280]">
                <th className="py-2.5 pr-3">Job</th>
                <th className="py-2.5 pr-3">Service</th>
                {role !== 'Client' && <th className="py-2.5 pr-3">Customer</th>}
                <th className="py-2.5 pr-3">For</th>
                <th className="py-2.5 pr-3">Status</th>
                <th className="py-2.5 pr-3">Progress</th>
                <th className="py-2.5 pr-3">Due</th>
                {role === 'Admin' && <th className="py-2.5 pr-3">Assigned</th>}
                {role === 'Admin' && <th className="py-2.5 pr-3 text-right">Fee</th>}
                <th className="py-2.5" aria-label="Actions" />
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E4E9]">
              {items.map((job) => {
                const status = JOB_STATUS[job.status] ?? { label: job.status, tone: 'neutral' }
                return (
                  <tr key={job.id} className="cursor-pointer align-top hover:bg-[#F7F8FA]" onClick={() => setSelectedId(job.id)}>
                    <td className="py-3 pr-3">
                      <p className="whitespace-nowrap font-medium tabular-nums text-[#16181D]">{job.jobNumber}</p>
                      {job.priority === 'Urgent' && <Chip tone="danger">Urgent</Chip>}
                    </td>
                    <td className="py-3 pr-3">
                      <p className="text-[#16181D]">{job.serviceName}</p>
                      <p className="text-xs text-[#6B7280]">{CATEGORY_LABELS[job.category]}</p>
                    </td>
                    {role !== 'Client' && (
                      <td className="py-3 pr-3">
                        <p className="text-[#16181D]">{job.clientName}</p>
                        <p className="text-xs text-[#6B7280]">{job.customerCode}</p>
                      </td>
                    )}
                    <td className="py-3 pr-3 text-[#16181D]">{job.personName ?? job.entityName ?? '—'}</td>
                    <td className="whitespace-nowrap py-3 pr-3"><StatusPill label={status.label} tone={status.tone} /></td>
                    <td className="py-3 pr-3 tabular-nums text-[#6B7280]">{job.stepsTotal ? `${job.stepsDone}/${job.stepsTotal}` : '—'}</td>
                    <td className="whitespace-nowrap py-3 pr-3">
                      <span className={job.isOverdue ? 'font-medium text-[#DC2626]' : 'text-[#16181D]'}>{formatDate(job.dueDate)}</span>
                    </td>
                    {role === 'Admin' && <td className="py-3 pr-3 text-[#16181D]">{job.assignedStaffName ?? <span className="text-[#D97706]">Unassigned</span>}</td>}
                    {role === 'Admin' && <td className="whitespace-nowrap py-3 pr-3 text-right tabular-nums">{formatAed(job.serviceFee)}</td>}
                    <td className="py-3 text-right">
                      <Button size="sm" variant="ghost" icon={Eye} aria-label={`Open ${job.jobNumber}`}
                        onClick={(e) => { e.stopPropagation(); setSelectedId(job.id) }} />
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>

        <ul className="space-y-2 md:hidden">
          {items.map((job) => {
            const status = JOB_STATUS[job.status] ?? { label: job.status, tone: 'neutral' }
            return (
              <li key={job.id}>
                <button type="button" onClick={() => setSelectedId(job.id)}
                  className="w-full cursor-pointer rounded-[10px] border border-[#E2E4E9] p-3 text-left hover:bg-[#F7F8FA]">
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium tabular-nums text-[#16181D]">{job.jobNumber}</span>
                    <StatusPill label={status.label} tone={status.tone} />
                  </div>
                  <p className="mt-1 text-sm text-[#16181D]">{job.serviceName}</p>
                  <p className="text-xs text-[#6B7280]">
                    {[role !== 'Client' ? job.clientName : null, job.personName, `Due ${formatDate(job.dueDate)}`].filter(Boolean).join(' · ')}
                  </p>
                </button>
              </li>
            )
          })}
        </ul>
      </>
    )
  }

  return (
    <div className="space-y-6">
      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-6">
        <KpiTile label="Open" value={summary?.openTotal} active={filter === 'open'} onClick={() => select('open')} />
        <KpiTile label="New" value={summary?.new} active={filter === 'new'} onClick={() => select('new')} />
        <KpiTile label="In progress" value={summary?.inProgress} tone="success" active={filter === 'progress'} onClick={() => select('progress')} />
        <KpiTile label="On hold" value={summary?.onHold} tone="warning" active={filter === 'hold'} onClick={() => select('hold')} />
        <KpiTile label="Overdue" value={summary?.overdue} tone="danger" active={filter === 'overdue'} onClick={() => select('overdue')} />
        {role === 'Admin'
          ? <KpiTile label="Unassigned" value={summary?.unassigned} tone="warning" active={filter === 'unassigned'} onClick={() => select('unassigned')} />
          : <KpiTile label="Completed today" value={summary?.completedToday} tone="success" />}
      </div>

      <SectionCard
        title={title}
        icon={Briefcase}
        subtitle={isStaff ? 'Click a job to work on it.' : 'Track the progress of the services we handle for you.'}
        action={
          <div className="flex items-center gap-2">
            <Button size="sm" icon={RefreshCw} loading={listQuery.isFetching && !listQuery.isLoading}
              onClick={() => { listQuery.refetch(); summaryQuery.refetch() }}>
              Refresh
            </Button>
            {headerAction}
          </div>
        }
      >
        <div className="mb-4 flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex flex-wrap gap-1.5" role="tablist" aria-label="Filter jobs">
            {QUICK_FILTERS.filter((f) => !f.adminOnly || role === 'Admin').map((f) => (
              <button key={f.key} type="button" role="tab" aria-selected={filter === f.key} onClick={() => select(f.key)}
                className={`cursor-pointer rounded-[8px] px-2.5 py-1 text-xs font-semibold transition duration-150 ${
                  filter === f.key ? 'bg-[#1C1F26] text-white' : 'bg-[#F7F8FA] text-[#6B7280] hover:text-[#16181D]'
                }`}>
                {f.key === 'overdue' && <AlertTriangle size={11} className="mr-1 inline" aria-hidden="true" />}
                {f.label}
              </button>
            ))}
          </div>
          <div className="flex flex-col gap-2 sm:flex-row">
            {isStaff && (
              <Select aria-label="Category" className="sm:w-48" value={category} onChange={(e) => { setCategory(e.target.value); setPage(1) }}>
                <option value="">All categories</option>
                {CATEGORIES.map((c) => <option key={c} value={c}>{CATEGORY_LABELS[c]}</option>)}
              </Select>
            )}
            <div className="relative sm:w-72">
              <Search size={14} className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]" aria-hidden="true" />
              <Input className="pl-8" aria-label="Search jobs" placeholder="Job no., customer, person, passport…"
                value={search} onChange={(e) => setSearch(e.target.value)} />
            </div>
          </div>
        </div>

        {content}

        {total > PAGE_SIZE && (
          <Pagination page={page} pageSize={PAGE_SIZE} totalCount={total} itemLabel="job" onPageChange={setPage} />
        )}
      </SectionCard>

      {selectedId && <JobDetailDrawer role={role} jobId={selectedId} onClose={() => setSelectedId(null)} />}
    </div>
  )
}

export default JobsBoard
