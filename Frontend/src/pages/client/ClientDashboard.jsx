import { format } from 'date-fns'
import {
  AlertTriangle,
  Bell,
  Building2,
  Calendar,
  CheckCircle2,
  ClipboardList,
  CreditCard,
  FileText,
  Mail,
  MapPin,
  Phone,
  Receipt,
  RefreshCw,
  ShieldCheck,
} from 'lucide-react'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import EmptyState from '../../components/client/EmptyState'
import ErrorState from '../../components/client/ErrorState'
import StatusPill from '../../components/client/StatusPill'
import {
  DOCUMENT_TYPES,
  PAYMENT_INVOICE_TYPES,
  PAYMENT_STATUS,
  RENEWAL_TASK_STATUS,
  SERVICE_CONTRACT_STATUS,
  SERVICE_REQUEST_STATUS,
  SERVICE_REQUEST_TYPES,
  enumLabel,
} from '../../components/client/enumLabels'
import {
  expiringStatusLabel,
  expiringStatusTone,
} from '../../components/client/documents/expiringStatus'
import { useClientCompany } from '../../hooks/client/useClientCompany'
import { useActiveContract } from '../../hooks/client/useActiveContract'
import { useExpiringDocuments } from '../../hooks/client/useExpiringDocuments'
import { useRenewalTasks } from '../../hooks/client/useRenewalTasks'
import { useServiceRequests } from '../../hooks/client/useServiceRequests'
import { usePaymentHistory } from '../../hooks/client/usePaymentHistory'
import { useUnreadNotificationCount } from '../../hooks/client/useUnreadNotificationCount'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function formatMoney(amount, currency = 'AED') {
  try {
    return new Intl.NumberFormat('en-AE', { style: 'currency', currency }).format(amount)
  } catch {
    return `${amount} ${currency}`
  }
}

function paymentStatusTone(status) {
  if (status === 3) return 'success'
  if (status === 4 || status === 5) return 'danger'
  return 'warning'
}

/* -------------------------------------------------------------------------
 * 1. HEADER AREA
 * ------------------------------------------------------------------------- */
function DashboardHeader() {
  const { data: company } = useClientCompany()
  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <div className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
      <div>
        <div className="flex items-center gap-2.5">
          <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">
            Dashboard
          </h1>
          <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
            <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" />
            Client Portal
          </span>
        </div>
        <p className="mt-1.5 text-sm text-[#6B7280]">
          {company?.companyName ? (
            <>
              Managing corporate compliance & PRO services for{' '}
              <span className="font-semibold text-[#16181D]">{company.companyName}</span>.
            </>
          ) : (
            'Welcome to the FASCO PRO Service Client Portal. Overview of compliance and active services.'
          )}
        </p>
      </div>

      <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
        <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
          <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
          <span className="font-medium text-[#16181D]">{today}</span>
        </div>
      </div>
    </div>
  )
}

/* -------------------------------------------------------------------------
 * 2. SUMMARY / ALERT AREA
 * ------------------------------------------------------------------------- */
function SummaryAlertArea() {
  const { data: docsData, isLoading: docsLoading, isError: docsError } = useExpiringDocuments({ days: 30 })
  const { data: tasksData, isLoading: tasksLoading, isError: tasksError } = useRenewalTasks({ page: 1, pageSize: 5 })
  const { data: unreadCount, isLoading: unreadLoading, isError: unreadError } = useUnreadNotificationCount()

  const docCount = docsData?.totalCount ?? 0
  const taskCount = tasksData?.totalCount ?? 0
  const unread = unreadCount ?? 0

  return (
    <section aria-label="Operational Summary & Alerts">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        {/* Expiring Documents Metric */}
        <div className="rounded-[12px] bg-white border border-[#E2E4E9] p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)] flex flex-col justify-between">
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] ${
                docCount > 0
                  ? 'bg-amber-50 text-[#D97706] border border-[#D97706]/20'
                  : 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20'
              }`}>
                {docCount > 0 ? (
                  <AlertTriangle size={18} strokeWidth={1.75} aria-hidden="true" />
                ) : (
                  <FileText size={18} strokeWidth={1.75} aria-hidden="true" />
                )}
              </div>
              <div>
                <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">
                  Expiring Documents
                </p>
                <p className="text-xs text-[#9CA3AF]">Next 30 days window</p>
              </div>
            </div>
            <div>
              {docsLoading ? (
                <span className="text-xs text-[#9CA3AF]">Checking…</span>
              ) : docsError ? (
                <StatusPill label="Unavailable" tone="danger" />
              ) : docCount > 0 ? (
                <StatusPill label="Action Due" tone="warning" />
              ) : (
                <StatusPill label="All Current" tone="success" />
              )}
            </div>
          </div>

          <div className="mt-4 flex items-baseline justify-between">
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-bold tracking-tight text-[#16181D]">
                {docsLoading ? '—' : docCount}
              </span>
              <span className="text-xs text-[#6B7280]">
                {docCount === 1 ? 'document' : 'documents'}
              </span>
            </div>
            <span className="text-xs font-medium text-[#6B7280]">
              {docCount > 0 ? 'Requires renewal' : 'No upcoming expiries'}
            </span>
          </div>
        </div>

        {/* Renewal Tasks Pipeline */}
        <div className="rounded-[12px] bg-white border border-[#E2E4E9] p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)] flex flex-col justify-between">
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
                <RefreshCw size={18} strokeWidth={1.75} className="text-[#16181D]" aria-hidden="true" />
              </div>
              <div>
                <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">
                  Renewal Tasks
                </p>
                <p className="text-xs text-[#9CA3AF]">Active pipeline workflow</p>
              </div>
            </div>
            <div>
              {tasksLoading ? (
                <span className="text-xs text-[#9CA3AF]">Checking…</span>
              ) : tasksError ? (
                <StatusPill label="Unavailable" tone="danger" />
              ) : taskCount > 0 ? (
                <StatusPill label="In Progress" tone="warning" />
              ) : (
                <StatusPill label="Clear" tone="success" />
              )}
            </div>
          </div>

          <div className="mt-4 flex items-baseline justify-between">
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-bold tracking-tight text-[#16181D]">
                {tasksLoading ? '—' : taskCount}
              </span>
              <span className="text-xs text-[#6B7280]">
                {taskCount === 1 ? 'active task' : 'active tasks'}
              </span>
            </div>
            <span className="text-xs font-medium text-[#6B7280]">
              {taskCount > 0 ? 'Processing updates' : 'No pending tasks'}
            </span>
          </div>
        </div>

        {/* Unread Notifications Alert */}
        <div className="rounded-[12px] bg-white border border-[#E2E4E9] p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)] flex flex-col justify-between sm:col-span-2 lg:col-span-1">
          <div className="flex items-start justify-between gap-3">
            <div className="flex items-center gap-3">
              <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] ${
                unread > 0
                  ? 'bg-amber-50 text-[#D97706] border border-[#D97706]/20'
                  : 'bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]'
              }`}>
                <Bell size={18} strokeWidth={1.75} aria-hidden="true" />
              </div>
              <div>
                <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">
                  Notifications
                </p>
                <p className="text-xs text-[#9CA3AF]">Compliance alerts</p>
              </div>
            </div>
            <div>
              {unreadLoading ? (
                <span className="text-xs text-[#9CA3AF]">Checking…</span>
              ) : unreadError ? (
                <StatusPill label="Unavailable" tone="danger" />
              ) : unread > 0 ? (
                <StatusPill label="Unread" tone="warning" />
              ) : (
                <StatusPill label="Caught Up" tone="neutral" />
              )}
            </div>
          </div>

          <div className="mt-4 flex items-baseline justify-between">
            <div className="flex items-baseline gap-2">
              <span className="text-3xl font-bold tracking-tight text-[#16181D]">
                {unreadLoading ? '—' : unread}
              </span>
              <span className="text-xs text-[#6B7280]">
                {unread === 1 ? 'new notification' : 'new notifications'}
              </span>
            </div>
            <span className="text-xs font-medium text-[#6B7280]">
              {unread > 0 ? 'Requires attention' : 'All alerts reviewed'}
            </span>
          </div>
        </div>
      </div>
    </section>
  )
}

/* -------------------------------------------------------------------------
 * 3. MAIN INFORMATION AREA: ACTIVE CONTRACT
 * ------------------------------------------------------------------------- */
function ContractCard() {
  const { data, isLoading, isError, refetch } = useActiveContract()

  let content
  if (isLoading) {
    content = <LoadingState label="Loading active contract…" />
  } else if (isError) {
    content = <ErrorState message="Could not load the active contract." onRetry={() => refetch()} />
  } else if (!data) {
    content = (
      <EmptyState
        icon={RefreshCw}
        message="No active contract found."
        description="There is currently no active PRO retainer contract associated with your account."
      />
    )
  } else {
    const startDate = formatDate(data.startDate)
    const endDate = formatDate(data.endDate)

    content = (
      <div className="space-y-4">
        {/* Retainer & Contract Identity Box */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <span className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">
              Contract Agreement
            </span>
            <p className="text-lg font-bold tracking-tight text-[#16181D]">
              {data.contractNumber}
            </p>
          </div>
          <div className="flex items-center gap-3">
            <div className="text-left sm:text-right">
              <span className="text-xs font-medium text-[#6B7280]">Retainer Fee</span>
              <p className="text-base font-bold text-[#0F9D74]">
                {formatMoney(data.retainerAmount, 'AED')}
              </p>
            </div>
            <StatusPill
              label={enumLabel(SERVICE_CONTRACT_STATUS, data.status) ?? 'Active'}
              tone="success"
            />
          </div>
        </div>

        {/* Contract Schedule & Validity */}
        <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 text-xs">
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
            <span className="text-[#6B7280]">Contract Commencement</span>
            <p className="mt-1 font-semibold text-[#16181D]">{startDate ?? 'N/A'}</p>
          </div>
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
            <span className="text-[#6B7280]">Contract Expiry</span>
            <p className="mt-1 font-semibold text-[#16181D]">{endDate ?? 'N/A'}</p>
          </div>
        </div>

        {/* Terms & Conditions */}
        {data.terms && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <span className="font-medium text-[#6B7280]">Scope & Terms:</span>
            <p className="mt-1 text-[#16181D] leading-relaxed">{data.terms}</p>
          </div>
        )}
      </div>
    )
  }

  return (
    <SectionCard
      title="Active Contract"
      icon={CheckCircle2}
      subtitle="Corporate PRO service agreement & retainer terms"
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 4. MAIN INFORMATION AREA: SERVICE REQUESTS
 * ------------------------------------------------------------------------- */
function ServiceRequestsCard() {
  const { data, isLoading, isError, refetch } = useServiceRequests({ page: 1, pageSize: 5 })

  let content
  if (isLoading) {
    content = <LoadingState label="Loading service requests…" />
  } else if (isError) {
    content = <ErrorState message="Could not load service requests." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={ClipboardList}
        message="No service requests on file."
        description="You have not submitted any new visa or PRO service requests recently."
      />
    )
  } else {
    const toneFor = (status) => {
      if (status === 'Converted') return 'success'
      if (status === 'Rejected') return 'danger'
      return 'warning'
    }

    content = (
      <div className="space-y-3">
        <ul className="divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.map((request) => (
            <li
              key={request.id}
              className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between first:pt-3 last:pb-3"
            >
              <div className="min-w-0 flex-1">
                <div className="flex items-center gap-2">
                  <p className="truncate text-sm font-semibold text-[#16181D]">
                    {enumLabel(SERVICE_REQUEST_TYPES, request.type) ?? 'PRO Service Request'}
                  </p>
                </div>
                <div className="mt-0.5 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-[#6B7280]">
                  <span>Submitted: {formatDate(request.createdAt) ?? 'Recent'}</span>
                  {request.description && (
                    <span className="truncate max-w-md text-[#6B7280] italic">
                      "{request.description}"
                    </span>
                  )}
                </div>
              </div>
              <div className="shrink-0 self-start sm:self-center">
                <StatusPill
                  label={enumLabel(SERVICE_REQUEST_STATUS, request.status) ?? 'Submitted'}
                  tone={toneFor(request.status)}
                />
              </div>
            </li>
          ))}
        </ul>

        {data.totalCount != null && (
          <p className="text-right text-xs text-[#9CA3AF]">
            Showing {data.items.length} of {data.totalCount} request{data.totalCount === 1 ? '' : 's'}
          </p>
        )}
      </div>
    )
  }

  return (
    <SectionCard
      title="Recent Service Requests"
      icon={ClipboardList}
      subtitle="Corporate visa applications & ad-hoc PRO requests"
      badge={
        data?.totalCount != null && (
          <span className="rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 5. MAIN INFORMATION AREA: PAYMENT & BILLING HISTORY
 * ------------------------------------------------------------------------- */
function PaymentsCard() {
  const { data, isLoading, isError, refetch } = usePaymentHistory({ page: 1, pageSize: 5 })

  let content
  if (isLoading) {
    content = <LoadingState label="Loading payment history…" />
  } else if (isError) {
    content = <ErrorState message="Could not load payment history." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={Receipt}
        message="No payment history recorded."
        description="Settled invoices and transaction receipts will be displayed here once processed."
      />
    )
  } else {
    const paidOrders = data.items.filter((order) => Number(order.status) === 3)
    const paidTotal = paidOrders.reduce((sum, order) => sum + order.amount, 0)

    content = (
      <div className="space-y-3">
        {/* Payment Summary Header */}
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] px-3.5 py-2.5 text-xs">
          <span className="text-[#6B7280]">
            <span className="font-semibold text-[#16181D]">{data.totalCount}</span> total payment{data.totalCount === 1 ? '' : 's'} recorded
          </span>
          {paidOrders.length > 0 && (
            <span className="font-medium text-[#16181D]">
              Total Paid: <span className="font-bold text-[#0F9D74]">{formatMoney(paidTotal, 'AED')}</span>
            </span>
          )}
        </div>

        <ul className="divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.map((order) => {
            const date = order.paidAt ?? order.failedAt ?? order.cancelledAt ?? order.createdAt
            return (
              <li
                key={order.id}
                className="flex flex-col gap-2 py-3 sm:flex-row sm:items-center sm:justify-between first:pt-3 last:pb-3"
              >
                <div className="min-w-0">
                  <p className="text-sm font-semibold text-[#16181D]">
                    {formatMoney(order.amount, order.currency)}
                  </p>
                  <p className="mt-0.5 truncate text-xs text-[#6B7280]">
                    {enumLabel(PAYMENT_INVOICE_TYPES, order.invoiceType) ?? 'Invoice'}
                    {date && ` · ${formatDate(date)}`}
                  </p>
                </div>
                <div className="shrink-0 self-start sm:self-center">
                  <StatusPill
                    label={enumLabel(PAYMENT_STATUS, order.status) ?? 'Pending'}
                    tone={paymentStatusTone(Number(order.status))}
                  />
                </div>
              </li>
            )
          })}
        </ul>

        {data.totalCount != null && (
          <p className="text-right text-xs text-[#9CA3AF]">
            Showing {data.items.length} of {data.totalCount} payment{data.totalCount === 1 ? '' : 's'}
          </p>
        )}
      </div>
    )
  }

  return (
    <SectionCard
      title="Payment & Billing"
      icon={CreditCard}
      subtitle="Retainer invoices and PRO fee settlement records"
      badge={
        data?.totalCount != null && (
          <span className="rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 6. SUMMARY & COMPLIANCE AREA: EXPIRING DOCUMENTS
 * ------------------------------------------------------------------------- */
function ExpiringDocumentsCard() {
  const { data, isLoading, isError, refetch } = useExpiringDocuments({ days: 30 })

  let content
  if (isLoading) {
    content = <LoadingState label="Checking expiring documents…" />
  } else if (isError) {
    content = <ErrorState message="Could not load expiring documents." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={FileText}
        message="No documents expiring in the next 30 days."
        description="All corporate licenses, visas, and registered cards are currently up to date."
      />
    )
  } else {
    content = (
      <div className="space-y-3">
        <div className="flex items-center justify-between rounded-[8px] bg-amber-50/60 border border-amber-200 px-3.5 py-2 text-xs text-[#D97706]">
          <div className="flex items-center gap-2">
            <AlertTriangle size={14} className="shrink-0 text-[#D97706]" aria-hidden="true" />
            <span className="font-medium">
              {data.totalCount} document{data.totalCount === 1 ? '' : 's'} require timely renewal
            </span>
          </div>
          <span className="text-[11px] font-semibold uppercase tracking-wider">30 Days</span>
        </div>

        <ul className="divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.slice(0, 5).map((doc) => {
            return (
              <li
                key={doc.id}
                className="flex flex-col gap-2.5 py-3 sm:flex-row sm:items-center sm:justify-between first:pt-3 last:pb-3"
              >
                <div className="min-w-0 flex-1">
                  <div className="flex items-center gap-2 flex-wrap">
                    <p className="text-sm font-semibold text-[#16181D]">
                      {enumLabel(DOCUMENT_TYPES, doc.type) ?? 'Official Document'}
                    </p>
                    <span className="rounded-[4px] bg-[#F7F8FA] px-1.5 py-0.5 text-[11px] font-mono font-medium text-[#6B7280] border border-[#E2E4E9]">
                      {doc.documentNumber}
                    </span>
                  </div>
                  <p className="mt-0.5 text-xs text-[#6B7280]">
                    {doc.expiryDate ? `Expires on ${formatDate(doc.expiryDate)}` : 'Expiry date pending'}
                  </p>
                </div>

                <div className="flex shrink-0 items-center gap-2 self-start sm:self-center">
                  <span className="inline-flex items-center rounded-[6px] px-2 py-0.5 text-xs font-semibold bg-amber-50 text-[#D97706] border border-[#D97706]/20">
                    {doc.daysRemaining}d left
                  </span>
                  <StatusPill
                    label={expiringStatusLabel(doc.status)}
                    tone={expiringStatusTone(doc.status)}
                  />
                </div>
              </li>
            )
          })}
        </ul>

        {data.totalCount != null && (
          <p className="text-right text-xs text-[#9CA3AF]">
            Showing {Math.min(data.items.length, 5)} of {data.totalCount} expiring document{data.totalCount === 1 ? '' : 's'}
          </p>
        )}
      </div>
    )
  }

  return (
    <SectionCard
      title="Expiring Documents"
      icon={FileText}
      subtitle="Critical compliance documents approaching expiry"
      badge={
        data?.totalCount != null && data.totalCount > 0 && (
          <span className="rounded-[6px] bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706] border border-[#D97706]/20">
            {data.totalCount} urgent
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 7. SUMMARY & COMPLIANCE AREA: RENEWAL TASKS
 * ------------------------------------------------------------------------- */
function RenewalTasksCard() {
  const { data, isLoading, isError, refetch } = useRenewalTasks({ page: 1, pageSize: 5 })

  let content
  if (isLoading) {
    content = <LoadingState label="Loading renewal tasks…" />
  } else if (isError) {
    content = <ErrorState message="Could not load renewal tasks." onRetry={() => refetch()} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={RefreshCw}
        message="No active renewal tasks."
        description="Your renewal workflow is current. Tasks will appear here automatically when documents enter renewal."
      />
    )
  } else {
    const toneFor = (status) => {
      if (status === 'Blocked') return 'danger'
      if (status === 'Approved' || status === 'Updated') return 'success'
      return 'warning'
    }

    content = (
      <div className="space-y-3">
        <ul className="divide-y divide-[#E2E4E9] border-t border-b border-[#E2E4E9]">
          {data.items.map((task) => {
            const isBlocked = task.status === 'Blocked'
            return (
              <li
                key={task.id}
                className="flex flex-col gap-2 py-3 first:pt-3 last:pb-3"
              >
                <div className="flex flex-col gap-1.5 sm:flex-row sm:items-center sm:justify-between">
                  <div className="min-w-0">
                    <p className="text-sm font-semibold text-[#16181D]">
                      {enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'Renewal Task'}
                    </p>
                    <p className="text-xs text-[#6B7280]">
                      Started: {formatDate(task.createdAt) ?? 'In queue'}
                      {task.completedAt && ` · Completed ${formatDate(task.completedAt)}`}
                    </p>
                  </div>
                  <div className="shrink-0 self-start sm:self-center">
                    <StatusPill
                      label={enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'Pending'}
                      tone={toneFor(task.status)}
                    />
                  </div>
                </div>

                {/* High visibility blocked reason if blocked */}
                {isBlocked && task.blockedReason && (
                  <div className="flex items-start gap-2 rounded-[6px] bg-red-50 p-2 text-xs font-medium text-[#DC2626] border border-red-200">
                    <AlertTriangle size={14} className="shrink-0 text-[#DC2626] mt-0.5" aria-hidden="true" />
                    <span>Action required: {task.blockedReason}</span>
                  </div>
                )}
              </li>
            )
          })}
        </ul>

        {data.totalCount != null && (
          <p className="text-right text-xs text-[#9CA3AF]">
            Showing {data.items.length} of {data.totalCount} task{data.totalCount === 1 ? '' : 's'} in workflow
          </p>
        )}
      </div>
    )
  }

  return (
    <SectionCard
      title="Renewal Tasks Pipeline"
      icon={RefreshCw}
      subtitle="Government portal filings & clearance workflow"
      badge={
        data?.totalCount != null && (
          <span className="rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 8. SUMMARY & COMPLIANCE AREA: NOTIFICATIONS
 * ------------------------------------------------------------------------- */
function NotificationsCard() {
  const { data: unread, isLoading, isError, refetch } = useUnreadNotificationCount()

  const unreadCount = unread ?? 0

  let content
  if (isLoading) {
    content = <LoadingState label="Loading notifications…" />
  } else if (isError) {
    content = <ErrorState message="Could not load notifications." onRetry={() => refetch()} />
  } else if (unreadCount === 0) {
    content = (
      <EmptyState
        icon={Bell}
        message="No unread notifications."
        description="All compliance alerts have been reviewed. New alerts will appear here automatically."
      />
    )
  } else {
    content = (
      <div className="flex flex-col gap-3 sm:flex-row sm:items-end sm:justify-between">
        <div>
          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-bold tracking-tight text-[#16181D]">{unreadCount}</span>
            <span className="text-xs text-[#6B7280]">
              {unreadCount === 1 ? 'new notification' : 'new notifications'}
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Compliance alerts & updates awaiting your review.
          </p>
        </div>
        <div className="shrink-0 self-start sm:self-center">
          <StatusPill label="Unread" tone="warning" />
        </div>
      </div>
    )
  }

  return (
    <SectionCard
      title="Notifications"
      icon={Bell}
      subtitle="Compliance alerts & unread updates"
      badge={
        unreadCount > 0 && (
          <span className="rounded-[6px] bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706] border border-[#D97706]/20">
            {unreadCount} unread
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * 9. SUPPORTING INFORMATION: COMPANY PROFILE
 * ------------------------------------------------------------------------- */
function CompanyCard() {
  const { data, isLoading, isError, refetch } = useClientCompany()

  let content
  if (isLoading) {
    content = <LoadingState label="Loading company profile…" />
  } else if (isError) {
    content = <ErrorState message="Could not load company information." onRetry={() => refetch()} />
  } else if (!data) {
    content = (
      <EmptyState
        icon={Building2}
        message="Company profile is not available."
        description="Corporate entity records could not be retrieved."
      />
    )
  } else {
    content = (
      <>
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between pb-5 border-b border-[#E2E4E9]">
          <div className="flex items-center gap-3.5">
            <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-[10px] bg-[#1C1F26] text-[#0F9D74] shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
              <Building2 size={22} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div>
              <div className="flex items-center gap-2 flex-wrap">
                <h2 className="text-lg font-bold tracking-tight text-[#16181D]">
                  {data.companyName}
                </h2>
                <span className="inline-flex items-center gap-1 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
                  <ShieldCheck size={12} aria-hidden="true" />
                  Verified Corporate Entity
                </span>
              </div>
              <p className="mt-0.5 text-xs text-[#6B7280]">
                Registered Client Organization & Compliance Account
              </p>
            </div>
          </div>
        </div>

        {/* Structured Corporate Metadata Grid */}
        <div className="mt-5 grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
          <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
            <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider block">
              Trade License
            </span>
            <p className="mt-1 text-sm font-semibold text-[#16181D] font-mono truncate">
              {data.tradeLicenseNumber ?? 'Not Registered'}
            </p>
          </div>

          <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
            <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider block">
              Emirate
            </span>
            <div className="mt-1 flex items-center gap-1.5">
              <MapPin size={13} className="text-[#6B7280] shrink-0" aria-hidden="true" />
              <p className="text-sm font-semibold text-[#16181D] truncate">
                {data.emirate ?? 'United Arab Emirates'}
              </p>
            </div>
          </div>

          <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
            <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider block">
              Phone
            </span>
            <div className="mt-1 flex items-center gap-1.5">
              <Phone size={13} className="text-[#6B7280] shrink-0" aria-hidden="true" />
              <p className="text-sm font-semibold text-[#16181D] truncate">
                {data.phone ?? 'N/A'}
              </p>
            </div>
          </div>

          <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
            <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider block">
              Email
            </span>
            <div className="mt-1 flex items-center gap-1.5">
              <Mail size={13} className="text-[#6B7280] shrink-0" aria-hidden="true" />
              <p className="text-sm font-semibold text-[#16181D] truncate">
                {data.email ?? 'N/A'}
              </p>
            </div>
          </div>

          <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3 sm:col-span-2 lg:col-span-1">
            <span className="text-[11px] font-medium text-[#6B7280] uppercase tracking-wider block">
              Office Address
            </span>
            <p className="mt-1 text-xs font-semibold text-[#16181D] truncate" title={data.address}>
              {data.address ?? 'Registered UAE Address'}
            </p>
          </div>
        </div>
      </>
    )
  }

  return (
    <SectionCard title="Corporate Profile" icon={Building2}>
      {content}
    </SectionCard>
  )
}

/* -------------------------------------------------------------------------
 * MAIN DASHBOARD COMPONENT
 * ------------------------------------------------------------------------- */
function ClientDashboard() {
  return (
    <div className="space-y-6">
      {/* 1. Header Area */}
      <DashboardHeader />

      {/* 2. Company Profile (supporting information) */}
      <CompanyCard />

      {/* 3. Summary / Alert Area */}
      <SummaryAlertArea />

      {/* 4. Main Information Grid */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        {/* Left Column: Contracts, Service Requests, Payments (7 cols on lg) */}
        <div className="space-y-6 lg:col-span-7">
          <ContractCard />
          <ServiceRequestsCard />
          <PaymentsCard />
        </div>

        {/* Right Column: Notifications, Expiring Documents & Renewal Tasks (5 cols on lg) */}
        <div className="space-y-6 lg:col-span-5">
          <NotificationsCard />
          <ExpiringDocumentsCard />
          <RenewalTasksCard />
        </div>
      </div>
    </div>
  )
}

export default ClientDashboard