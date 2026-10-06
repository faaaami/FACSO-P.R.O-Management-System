import {
  Building,
  FileClock,
  Inbox,
  Landmark,
  ListFilter,
  ListTodo,
  OctagonAlert,
  Receipt,
  ScrollText,
  ShieldCheck,
  Users,
  UsersRound,
  Wallet,
} from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import DashboardWidget from '../../components/admin/dashboard/DashboardWidget'
import DashboardKpiCard from '../../components/admin/dashboard/DashboardKpiCard'
import DashboardDataList from '../../components/admin/dashboard/DashboardDataList'
import { useAuth } from '../../auth/AuthContext'
import { useAdminEntityMaps } from '../../hooks/admin/useAdminEntityMaps'
import {
  RENEWAL_TASK_STATUS,
  DOCUMENT_TYPES,
  enumLabel,
  documentStatusLabel,
  serviceRequestStatusLabel,
  serviceRequestTypeLabel,
} from '../../components/admin/enumLabels'
import {
  useAdminActivityFeed,
  useAdminDashboardPaidInvoiceCount,
  useAdminDashboardSummary,
  useAdminExpiringPreview,
  useAdminPendingInvoiceCounts,
  useAdminServiceRequestPreview,
  useAdminTaskStatusBreakdown,
} from '../../hooks/admin/useAdminDashboard'

/**
 * Admin Portal dashboard — Phase 1.
 *
 * Read-only, seven independent requests, no polling and no mutations. Three
 * rules govern everything on this page:
 *
 *  1. A failed request never looks like a number. KPIs fall back to an em dash
 *     plus "Unavailable" and offer a retry; widgets render an error with retry.
 *     Only a real zero renders as 0.
 *  2. Every figure is labelled with the scope it was actually measured in. The
 *     workload figures cover tasks ASSIGNED to agents, service-request totals
 *     are ALL-TIME rather than open, and the expiry figure looks FORWARD 30
 *     days rather than at overdue documents.
 *  3. Nothing is inferred. The expiry registry returns no owner names, so none
 *     are shown; no revenue panel is built, because no approved query supports
 *     one and an empty panel would be filler.
 *
 * REVENUE AMOUNTS ARE STILL NOT SHOWN, and the rule above is why. The Portfolio
 * band reads only `invoiceCount` from /dashboard/revenue — a plain count of paid
 * invoices. The same response's totalRevenue / retainerRevenue /
 * serviceFeeRevenue are cross-currency sums and are never rendered, so the
 * dashboard continues to display no money at all. This page has no revenue
 * widget because the amounts cannot be shown honestly, not because the endpoint
 * is unwrapped.
 */

const EXPIRY_WINDOW_DAYS = 30
const EXPIRY_PREVIEW_SIZE = 8
const SERVICE_REQUEST_PREVIEW_SIZE = 5
const ACTIVITY_PREVIEW_SIZE = 8

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

/**
 * Tones are decorative only — every status is always accompanied by its text
 * label, so the state is never communicated by colour alone.
 */
const TONE_CLASSES = {
  neutral: 'border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]',
  success: 'border-[#0F9D74]/25 bg-[rgba(15,157,116,0.10)] text-[#0B7A5A]',
  warning: 'border-[#B45309]/25 bg-[rgba(180,83,9,0.10)] text-[#92400E]',
  danger: 'border-[#DC2626]/25 bg-[rgba(220,38,38,0.10)] text-[#B91C1C]',
}

const RENEWAL_TASK_TONES = {
  Submitted: 'neutral',
  FeePaid: 'neutral',
  AwaitingApproval: 'warning',
  Blocked: 'danger',
  Approved: 'success',
  Updated: 'warning',
}

const SERVICE_REQUEST_TONES = {
  Submitted: 'neutral',
  Converted: 'success',
  Rejected: 'danger',
}

const DOCUMENT_TONES = {
  Active: 'neutral',
  ExpiringSoon: 'warning',
  Overdue: 'danger',
  InRenewal: 'neutral',
}

function StatusPill({ tone = 'neutral', children }) {
  return (
    <span
      className={`inline-flex items-center rounded-[6px] border px-2 py-0.5 text-xs font-semibold whitespace-nowrap ${TONE_CLASSES[tone] ?? TONE_CLASSES.neutral}`}
    >
      {children}
    </span>
  )
}

function greetingFor(name) {
  const hour = new Date().getHours()
  const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'
  return name ? `${part}, ${name}` : part
}

function firstNameOf(fullName) {
  const trimmed = String(fullName ?? '').trim()
  return trimmed ? trimmed.split(/\s+/)[0] : null
}

function formatDate(value) {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return '—'
  return parsed.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })
}

function formatDateTime(value) {
  if (!value) return '—'
  const parsed = new Date(value)
  if (Number.isNaN(parsed.getTime())) return '—'
  return `${parsed.toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' })} · ${parsed.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' })}`
}

function sumAssignedTasks(member) {
  return Object.values(member?.tasksByStatus ?? {}).reduce(
    (sum, value) => sum + (Number.isFinite(value) ? value : 0),
    0,
  )
}

/** Group heading for a band of KPIs. Keeps the two KPI rows landmark-navigable. */
/**
 * Column classes are written out in full rather than interpolated. Tailwind's
 * scanner only sees literal class strings, so `xl:grid-cols-${columns}` would be
 * purged from the build and every band would silently collapse to one column.
 */
const KPI_BAND_COLUMNS = {
  4: 'grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4',
  5: 'grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-5',
}

function KpiBand({ id, title, hint, columns = 4, children }) {
   return (
   <section aria-labelledby={id}>
   <div className="mb-3 flex flex-wrap items-baseline gap-x-2.5 gap-y-1">
   <h2 id={id} className="text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
   {title}
   </h2>
   {hint && <p className="text-xs text-[#9CA3AF]">{hint}</p>}
   </div>
   <div className={KPI_BAND_COLUMNS[columns] ?? KPI_BAND_COLUMNS[4]}>{children}</div>
   </section>
   )
   }

function DashboardHeader() {
  const { user } = useAuth()
  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  })

  return (
    <AdminPageHeader
      title={greetingFor(firstNameOf(user?.fullName))}
      subtitle={`${today} · Portfolio-wide operational overview`}
      action={ADMIN_PORTAL_BADGE}
    />
  )
}

/**
 * Row A — what needs a decision today. Each card links into the module that owns
 * the data, so a nonzero count is one click from the work that produced it.
 */
function NeedsAttentionBand() {
  const { blockedAssigned, loading: workloadLoading, error: workloadError, refresh: refreshWorkload } =
    useAdminTaskStatusBreakdown()
  const invoiceCounts = useAdminPendingInvoiceCounts()
  const expiring = useAdminExpiringPreview({
    days: EXPIRY_WINDOW_DAYS,
    pageSize: EXPIRY_PREVIEW_SIZE,
  })

  return (
    <KpiBand
      id="needs-attention-heading"
      title="Needs attention"
      hint="Counts that describe work waiting on someone."
    >
      <DashboardKpiCard
        label="Blocked tasks"
        icon={OctagonAlert}
        value={blockedAssigned}
        context="Assigned to staff and held up"
        href="/renewal-tasks"
        loading={workloadLoading}
        unavailable={Boolean(workloadError)}
        onRetry={refreshWorkload}
      />
      <DashboardKpiCard
        label="Retainer invoices pending"
        icon={Receipt}
        value={invoiceCounts.retainer.count}
        context="Awaiting payment"
        href="/invoices"
        loading={invoiceCounts.retainer.loading}
        unavailable={Boolean(invoiceCounts.retainer.error)}
        onRetry={invoiceCounts.retainer.refresh}
      />
      <DashboardKpiCard
        label="Service-fee invoices pending"
        icon={Wallet}
        value={invoiceCounts.serviceFee.count}
        context="Awaiting payment"
        href="/invoices"
        loading={invoiceCounts.serviceFee.loading}
        unavailable={Boolean(invoiceCounts.serviceFee.error)}
        onRetry={invoiceCounts.serviceFee.refresh}
      />
      <DashboardKpiCard
        label="Documents expiring"
        icon={FileClock}
        value={expiring.totalCount}
        context={`Expiring within ${EXPIRY_WINDOW_DAYS} days`}
        href="/documents"
        loading={expiring.loading}
        unavailable={Boolean(expiring.error)}
        onRetry={expiring.refresh}
      />
    </KpiBand>
  )
}

/** Row B — portfolio scale. Informational only; no navigation was specified. */
function PortfolioBand() {
  const summary = useAdminDashboardSummary()

  const unavailable = Boolean(summary.error) || summary.loading

  /*
   * The paid-invoice count is a separate request, so it carries its own loading
   * and error state rather than joining `unavailable`. Folding it into the shared
   * flag would make a failing /dashboard/summary blank a count that loaded
   * correctly, and vice versa — the one-promise-one-card rule this page is built
   * on. The money fields in the same response are NOT read here; see
   * useAdminDashboardPaidInvoiceCount for why.
   *
   * The card says "last 30 days", not "this month", because that is what the
   * backend measures: GetRevenueAsync sets periodStart to DateTime.UtcNow
   * .AddMonths(-1) for the 'month' period, a rolling 30-day window that has no
   * relationship to calendar-month boundaries. A 'week' period is likewise
   * AddDays(-7), not the current calendar week. "This month" would be a false
   * claim about the window, so the wording names the rolling range.
   */
  const paidInvoices = useAdminDashboardPaidInvoiceCount({ period: 'month' })

  return (
    <KpiBand
      id="portfolio-heading"
      title="Portfolio"
      columns={5}
      hint={`Active renewal tasks counts Submitted, Fee Paid, Awaiting Approval and Approved — not Blocked or Updated.`}
    >
      <DashboardKpiCard
        label="Client companies"
        icon={Building}
        value={summary.clientCompanies}
        loading={summary.loading}
        unavailable={unavailable}
        onRetry={summary.refresh}
      />
      <DashboardKpiCard
        label="Legal entities"
        icon={Landmark}
        value={summary.legalEntities}
        loading={summary.loading}
        unavailable={unavailable}
        onRetry={summary.refresh}
      />
      <DashboardKpiCard
        label="Employees"
        icon={Users}
        value={summary.employees}
        loading={summary.loading}
        unavailable={unavailable}
        onRetry={summary.refresh}
      />
      <DashboardKpiCard
        label="Active renewal tasks"
        icon={ListTodo}
        value={summary.activeRenewalTasks}
        context="Portfolio-wide, all assignees"
        loading={summary.loading}
        unavailable={unavailable}
        onRetry={summary.refresh}
      />
      <DashboardKpiCard
        label="Paid invoices"
        icon={Receipt}
        value={paidInvoices.invoiceCount}
        context="Retainer and service fee, last 30 days"
        loading={paidInvoices.loading}
        unavailable={Boolean(paidInvoices.error) || paidInvoices.loading}
        onRetry={paidInvoices.refresh}
      />
    </KpiBand>
  )
}

/**
 * Row C left — the six-status renewal pipeline, summed across all agents from
 * the same rows the per-agent table below shows. Zero in every status is a real,
 * healthy state, so it renders as zeros rather than an empty panel.
 */
function RenewalTaskWorkloadWidget() {
  const { byStatus, statusKeys, totalAssigned, loading, error, refresh } = useAdminTaskStatusBreakdown()
  const maxCount = Math.max(1, ...statusKeys.map((key) => byStatus[key] ?? 0))

  return (
    <DashboardWidget
      title="Renewal task workload"
      subtitle={`${totalAssigned.toLocaleString('en-US')} assigned across all six statuses`}
      icon={ListFilter}
      actionTo="/renewal-tasks"
      actionLabel="View tasks"
      loading={loading}
      loadingLabel="Loading task workload…"
      error={error}
      onRetry={refresh}
      errorMessage="Could not load the renewal task workload."
      isEmpty={!loading && !error && totalAssigned === 0}
      emptyMessage="No assigned renewal tasks"
      emptyDescription="Tasks appear here once they are assigned to an agent."
      emptyIcon={ListTodo}
    >
      <ul className="flex flex-col gap-3">
        {statusKeys.map((key) => {
          const count = byStatus[key] ?? 0
          const percentage = Math.round((count / maxCount) * 100)
          return (
            <li key={key} className="flex items-center gap-3">
              <span className="w-28 shrink-0 text-xs font-medium text-[#16181D] sm:w-32">
                {RENEWAL_TASK_STATUS[key]}
              </span>
              <span className="h-2 flex-1 overflow-hidden rounded-full bg-[#F0F1F4]">
                <span
                  className="block h-2 rounded-full bg-[#0F9D74] transition-[width] duration-150"
                  style={{ width: `${percentage}%` }}
                />
              </span>
              <span className="w-10 shrink-0 text-right text-xs font-semibold tabular-nums text-[#16181D]">
                {count.toLocaleString('en-US')}
              </span>
            </li>
          )
        })}
      </ul>
    </DashboardWidget>
  )
}

/** Row C right — the newest requests, with the all-time total called out as such. */
function ServiceRequestsWidget() {
  const { items, totalCount, loading, error, refresh } = useAdminServiceRequestPreview({
    pageSize: SERVICE_REQUEST_PREVIEW_SIZE,
  })
  const { resolveClient } = useAdminEntityMaps()

  const columns = [
    {
      key: 'client',
      header: 'Client company',
      primary: true,
      render: (row) => {
        const client = resolveClient(row?.clientCompanyId)
        if (!client) return 'Unlinked request'
        return client.resolved ? client.name : client.fallback
      },
    },
    {
      key: 'type',
      header: 'Type',
      render: (row) => serviceRequestTypeLabel(row?.type) ?? '—',
    },
    {
      key: 'status',
      header: 'Status',
      render: (row) => (
        <StatusPill tone={SERVICE_REQUEST_TONES[row?.status]}>
          {serviceRequestStatusLabel(row?.status) ?? 'Unknown'}
        </StatusPill>
      ),
    },
    {
      key: 'submitted',
      header: 'Submitted',
      render: (row) => formatDate(row?.createdAt),
    },
  ]

  return (
    <DashboardWidget
      title="Service requests"
      subtitle={
        totalCount == null
          ? 'Newest submissions'
          : `Newest submissions · All-time: ${totalCount.toLocaleString('en-US')}`
      }
      icon={Inbox}
      actionTo="/service-requests"
      actionLabel="View all"
      loading={loading}
      loadingLabel="Loading service requests…"
      error={error}
      onRetry={refresh}
      errorMessage="Could not load service requests."
      isEmpty={!loading && !error && items.length === 0}
      emptyMessage="No service requests yet"
      emptyDescription="Requests raised by client companies will appear here."
      emptyIcon={Inbox}
    >
      <DashboardDataList
        caption="Newest service requests"
        columns={columns}
        rows={items}
        errorMessage="Could not load service requests."
      />
    </DashboardWidget>
  )
}

/**
 * Row D — forward-looking compliance.
 *
 * This widget is deliberately full width and there is no second panel beside it.
 * The only other candidate data in the approved request set is the same expiry
 * list, so a second panel would either duplicate these rows or imply a
 * 31–90 day breakdown that no approved query actually supports. Empty space is
 * more honest than a filler widget.
 */
function ExpiringDocumentsWidget() {
  const { items, totalCount, loading, error, refresh } = useAdminExpiringPreview({
    days: EXPIRY_WINDOW_DAYS,
    pageSize: EXPIRY_PREVIEW_SIZE,
  })

  const columns = [
    {
      key: 'document',
      header: 'Document',
      primary: true,
      render: (row) => row?.documentNumber ?? '—',
    },
    {
      key: 'type',
      header: 'Type',
      render: (row) => enumLabel(DOCUMENT_TYPES, row?.type),
    },
    {
      // The registry returns only clientEntityId / employeeId GUIDs, and the
      // Admin API has no entity-name lookup, so the holder is shown by kind
      // rather than by an invented name.
      key: 'holder',
      header: 'Held by',
      render: (row) => (row?.employeeId ? 'Employee' : row?.clientEntityId ? 'Entity' : '—'),
    },
    {
      key: 'expiry',
      header: 'Expiry date',
      render: (row) => formatDate(row?.expiryDate),
    },
    {
      key: 'days',
      header: 'Days remaining',
      render: (row) => {
        const days = row?.daysRemaining
        if (!Number.isFinite(days)) return '—'
        const tone = days <= 7 ? 'danger' : days <= 14 ? 'warning' : 'neutral'
        return <StatusPill tone={tone}>{days} {days === 1 ? 'day' : 'days'}</StatusPill>
      },
    },
    {
      key: 'status',
      header: 'Status',
      render: (row) => (
        <StatusPill tone={DOCUMENT_TONES[row?.status]}>
          {documentStatusLabel(row?.status) ?? 'Unknown'}
        </StatusPill>
      ),
    },
  ]

  return (
    <DashboardWidget
      title="Expiring documents"
      subtitle={
        totalCount == null
          ? `Expiring within ${EXPIRY_WINDOW_DAYS} days`
          : `${totalCount.toLocaleString('en-US')} expiring within ${EXPIRY_WINDOW_DAYS} days · already-expired documents excluded`
      }
      icon={FileClock}
      actionTo="/documents"
      actionLabel="View all"
      loading={loading}
      loadingLabel="Loading expiring documents…"
      error={error}
      onRetry={refresh}
      errorMessage="Could not load expiring documents."
      isEmpty={!loading && !error && items.length === 0}
      emptyMessage={`No documents expiring within ${EXPIRY_WINDOW_DAYS} days`}
      emptyDescription="Documents appear here as they approach their expiry date."
      emptyIcon={FileClock}
    >
      <DashboardDataList
        caption={`Documents expiring within ${EXPIRY_WINDOW_DAYS} days`}
        columns={columns}
        rows={items}
        errorMessage="Could not load expiring documents."
      />
    </DashboardWidget>
  )
}

/** Row E left — per-agent assigned workload, resolved from the cached staff map. */
function StaffWorkloadWidget() {
  const { staff, loading, error, refresh } = useAdminTaskStatusBreakdown()
  const { resolveStaff } = useAdminEntityMaps()

  // Densest first: an agent carrying a backlog is the reason to look here. Ties
  // fall back to blocked count, then to the staff id for a stable order.
  const rows = [...staff].sort(
    (a, b) =>
      sumAssignedTasks(b) - sumAssignedTasks(a) ||
      (b?.blockedTaskCount ?? 0) - (a?.blockedTaskCount ?? 0) ||
      String(a?.staffId ?? '').localeCompare(String(b?.staffId ?? '')),
  )

  const columns = [
    {
      key: 'staff',
      header: 'Agent',
      primary: true,
      render: (row) => {
        const member = resolveStaff(row?.staffId)
        if (!member) return 'Unassigned'
        return member.resolved ? member.name : member.fallback
      },
    },
    {
      key: 'assigned',
      header: 'Assigned',
      align: 'right',
      render: (row) => sumAssignedTasks(row).toLocaleString('en-US'),
    },
    {
      key: 'active',
      header: 'Active',
      align: 'right',
      render: (row) => (row?.activeTaskCount ?? 0).toLocaleString('en-US'),
    },
    {
      key: 'blocked',
      header: 'Blocked',
      align: 'right',
      render: (row) => {
        const blocked = row?.blockedTaskCount ?? 0
        return blocked > 0 ? (
          <StatusPill tone="danger">{blocked.toLocaleString('en-US')}</StatusPill>
        ) : (
          <span className="text-xs text-[#6B7280]">0</span>
        )
      },
    },
  ]

  return (
    <DashboardWidget
      title="Staff workload"
      subtitle="Assigned renewal tasks per agent, busiest first · Active excludes Blocked and Updated"
      icon={UsersRound}
      actionTo="/staff"
      actionLabel="View staff"
      loading={loading}
      loadingLabel="Loading staff workload…"
      error={error}
      onRetry={refresh}
      errorMessage="Could not load staff workload."
      isEmpty={!loading && !error && rows.length === 0}
      emptyMessage="No agents to report on"
      emptyIcon={UsersRound}
    >
      <DashboardDataList
        caption="Assigned renewal tasks per agent"
        columns={columns}
        rows={rows}
        errorMessage="Could not load staff workload."
      />
    </DashboardWidget>
  )
}

/** Row E right — newest audit activity, with the acting user already joined. */
function RecentActivityWidget() {
  const { items, loading, error, refresh } = useAdminActivityFeed({ pageSize: ACTIVITY_PREVIEW_SIZE })

  const columns = [
    {
      key: 'activity',
      header: 'Activity',
      primary: true,
      render: (row) => row?.description ?? row?.action ?? '—',
    },
    {
      key: 'user',
      header: 'User',
      render: (row) => row?.userName ?? 'System',
    },
    {
      key: 'entity',
      header: 'Record type',
      render: (row) => row?.entityType ?? '—',
    },
    {
      key: 'when',
      header: 'When',
      render: (row) => formatDateTime(row?.createdAt),
    },
  ]

  return (
    <DashboardWidget
      title="Recent activity"
      subtitle="Newest entries from the audit log"
      icon={ScrollText}
      actionTo="/audit-log"
      actionLabel="View audit log"
      loading={loading}
      loadingLabel="Loading recent activity…"
      error={error}
      onRetry={refresh}
      errorMessage="Could not load recent activity."
      isEmpty={!loading && !error && items.length === 0}
      emptyMessage="No recent activity"
      emptyIcon={ScrollText}
    >
      <DashboardDataList
        caption="Newest audit log entries"
        columns={columns}
        rows={items}
        errorMessage="Could not load recent activity."
      />
    </DashboardWidget>
  )
}

function AdminDashboardPage() {
  return (
    <div className="space-y-6">
      <DashboardHeader />

      <NeedsAttentionBand />
      <PortfolioBand />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <RenewalTaskWorkloadWidget />
        <ServiceRequestsWidget />
      </div>

      <ExpiringDocumentsWidget />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <StaffWorkloadWidget />
        <RecentActivityWidget />
      </div>
    </div>
  )
}

export default AdminDashboardPage
