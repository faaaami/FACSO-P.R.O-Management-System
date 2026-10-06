import { AlertTriangle, CheckCircle2, ClipboardList, Hourglass } from 'lucide-react'

function SummaryMetric({ icon: Icon, label, hint, value, tone }) {
  const iconTone = {
    success: 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20',
    warning: 'bg-amber-50 text-[#D97706] border border-[#D97706]/20',
    danger: 'bg-red-50 text-[#DC2626] border border-[#DC2626]/20',
    neutral: 'bg-[#F7F8FA] text-[#6B7280] border border-[#E2E4E9]',
  }[tone]

  return (
    <div className="rounded-[12px] bg-white border border-[#E2E4E9] p-5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-center gap-3">
        {Icon && (
          <div className={`flex h-10 w-10 shrink-0 items-center justify-center rounded-[8px] ${iconTone}`}>
            <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
        )}
        <div>
          <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">{label}</p>
          <p className="text-xs text-[#9CA3AF]">{hint}</p>
        </div>
      </div>
      <div className="mt-4 flex items-baseline gap-2">
        <span className="text-3xl font-bold tracking-tight text-[#16181D]">{value}</span>
        <span className="text-xs text-[#6B7280]">tasks</span>
      </div>
    </div>
  )
}

function RenewalTaskSummaryCards({ total, awaitingApproval, blocked, approved }) {
  return (
    <section
      aria-label="Renewal Tasks Summary"
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
    >
      <SummaryMetric
        icon={ClipboardList}
        label="Total Tasks"
        hint="All renewal tasks on record"
        value={total}
        tone="neutral"
      />
      <SummaryMetric
        icon={Hourglass}
        label="Awaiting Approval"
        hint="Pending PRO approval"
        value={awaitingApproval}
        tone={awaitingApproval > 0 ? 'warning' : 'neutral'}
      />
      <SummaryMetric
        icon={AlertTriangle}
        label="Blocked"
        hint="Action required from you"
        value={blocked}
        tone={blocked > 0 ? 'danger' : 'neutral'}
      />
      <SummaryMetric
        icon={CheckCircle2}
        label="Approved"
        hint="Completed renewals"
        value={approved}
        tone={approved > 0 ? 'success' : 'neutral'}
      />
    </section>
  )
}

export default RenewalTaskSummaryCards