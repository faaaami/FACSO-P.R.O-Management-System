import { Link } from 'react-router-dom'
import { Bell, CalendarClock, ClipboardList, OctagonAlert } from 'lucide-react'

const TONE_CLASSES = {
  neutral: 'border-[#E2E4E9] bg-white text-[#16181D]',
  danger: 'border-red-200 bg-red-50/60 text-[#DC2626]',
  warning: 'border-amber-200 bg-amber-50/70 text-[#D97706]',
  success: 'border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] text-[#0F9D74]',
}

function KpiCard({ to, icon: Icon, label, value, tone = 'neutral', hint }) {
  return (
    <Link
      to={to}
      className={`flex items-center gap-3 rounded-[12px] border p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:shadow-[0_4px_12px_rgba(28,31,38,0.08)] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] ${TONE_CLASSES[tone]}`}
    >
      {Icon && (
        <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white">
          <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
        </div>
      )}
      <div className="min-w-0">
        <p className="text-2xl font-bold leading-none tracking-tight text-[#16181D]">{value}</p>
        <p className="mt-1 truncate text-xs font-medium text-[#6B7280]">{label}</p>
        {hint && <p className="truncate text-[11px] text-[#9CA3AF]">{hint}</p>}
      </div>
    </Link>
  )
}

/**
 * Top-of-dashboard KPI row. Each card links to the page that owns the number.
 * `unreadNotifications` is page-scoped (the notifications endpoint exposes no
 * global unread count), so it is labelled accordingly.
 */
function DashboardKpiRow({ activeTasks, blockedTasks, expiringSoon, unreadNotifications }) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 xl:grid-cols-4">
      <KpiCard
        to="/renewal-tasks"
        icon={ClipboardList}
        label="Active tasks"
        value={activeTasks}
        tone="neutral"
      />
      <KpiCard
        to="/renewal-tasks"
        icon={OctagonAlert}
        label="Blocked tasks"
        value={blockedTasks}
        tone={blockedTasks > 0 ? 'danger' : 'neutral'}
        hint={blockedTasks > 0 ? 'Needs your attention' : undefined}
      />
      <KpiCard
        to="/documents"
        icon={CalendarClock}
        label="Expiring in 30 days"
        value={expiringSoon}
        tone={expiringSoon > 0 ? 'warning' : 'neutral'}
      />
      <KpiCard
        to="/notifications"
        icon={Bell}
        label="Unread (this page)"
        value={unreadNotifications}
        tone={unreadNotifications > 0 ? 'warning' : 'neutral'}
      />
    </div>
  )
}

export default DashboardKpiRow
