const TONES = {
  success: {
    badge: 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border border-[#0F9D74]/20',
    dot: 'bg-[#0F9D74]',
  },
  warning: {
    badge: 'bg-amber-50 text-[#D97706] border border-[#D97706]/20',
    dot: 'bg-[#D97706]',
  },
  danger: {
    badge: 'bg-red-50 text-[#DC2626] border border-[#DC2626]/20',
    dot: 'bg-[#DC2626]',
  },
  neutral: {
    badge: 'bg-gray-100 text-[#6B7280] border border-[#E2E4E9]',
    dot: 'bg-[#9CA3AF]',
  },
}

function StatusPill({ label, tone = 'neutral' }) {
  const current = TONES[tone] ?? TONES.neutral

  return (
    <span className={`inline-flex items-center gap-1.5 rounded-[6px] px-2.5 py-0.5 text-xs font-medium shrink-0 ${current.badge}`}>
      <span className={`h-1.5 w-1.5 rounded-full shrink-0 ${current.dot}`} aria-hidden="true" />
      <span>{label}</span>
    </span>
  )
}

export default StatusPill