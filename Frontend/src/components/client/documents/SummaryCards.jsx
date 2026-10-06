import { AlertTriangle, FileText } from 'lucide-react'

function SummaryMetric({ icon: Icon, label, hint, value, tone }) {
  const iconTone = {
    success: 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20',
    warning: 'bg-amber-50 text-[#D97706] border border-[#D97706]/20',
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
        <span className="text-xs text-[#6B7280]">items on record</span>
      </div>
    </div>
  )
}

function SummaryCards({ total, expiring }) {
  return (
    <section aria-label="Documents Summary" className="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <SummaryMetric
        icon={FileText}
        label="Total Documents"
        hint="All company documents"
        value={total}
        tone="neutral"
      />
      <SummaryMetric
        icon={AlertTriangle}
        label="Expiring"
        hint="Next 30 days window"
        value={expiring}
        tone={expiring > 0 ? 'warning' : 'success'}
      />
    </section>
  )
}

export default SummaryCards