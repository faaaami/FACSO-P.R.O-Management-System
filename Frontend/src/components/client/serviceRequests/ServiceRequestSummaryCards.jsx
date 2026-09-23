import { CheckCircle2, ClipboardList, Clock, XCircle } from 'lucide-react'

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
        <span className="text-xs text-[#6B7280]">requests</span>
      </div>
    </div>
  )
}

function ServiceRequestSummaryCards({ total, submitted, converted, rejected }) {
  return (
    <section
      aria-label="Service Requests Summary"
      className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4"
    >
      <SummaryMetric
        icon={ClipboardList}
        label="Total Requests"
        hint="All requests on record"
        value={total}
        tone="neutral"
      />
      <SummaryMetric
        icon={Clock}
        label="Submitted"
        hint="Open for processing"
        value={submitted}
        tone={submitted > 0 ? 'warning' : 'neutral'}
      />
      <SummaryMetric
        icon={CheckCircle2}
        label="Converted"
        hint="Completed into a task"
        value={converted}
        tone={converted > 0 ? 'success' : 'neutral'}
      />
      <SummaryMetric
        icon={XCircle}
        label="Rejected"
        hint="Declined with a reason"
        value={rejected}
        tone={rejected > 0 ? 'danger' : 'neutral'}
      />
    </section>
  )
}

export default ServiceRequestSummaryCards