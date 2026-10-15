import { ArrowDownWideNarrow, ArrowUpNarrowWide, CircleDot } from 'lucide-react'

const SORT_OPTIONS = [
  { value: 'createdAt:desc', label: 'Newest', Icon: ArrowDownWideNarrow },
  { value: 'createdAt:asc', label: 'Oldest', Icon: ArrowUpNarrowWide },
  { value: 'isRead:asc', label: 'Unread first', Icon: CircleDot },
]

function AgentNotificationSort({ value, onChange, disabled = false }) {
  return (
    <div
      role="group"
      aria-label="Sort notifications"
      className="inline-flex w-full flex-wrap items-center gap-1 rounded-[10px] border border-[#E2E4E9] bg-white p-1 shadow-[0_1px_2px_rgba(28,31,38,0.04)] sm:w-auto"
    >
      {SORT_OPTIONS.map((option) => {
        const { value: optionValue, label } = option
        const Icon = option.Icon
        const isActive = optionValue === value

        return (
          <button
            key={optionValue}
            type="button"
            onClick={() => onChange(optionValue)}
            disabled={disabled}
            aria-pressed={isActive}
            className={`inline-flex flex-1 items-center justify-center gap-1.5 rounded-[8px] px-3 py-1.5 text-xs font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50 sm:flex-none ${
              isActive
                ? 'bg-[#0F9D74] text-white'
                : 'text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D]'
            }`}
          >
            <Icon size={13} strokeWidth={2} aria-hidden="true" />
            {label}
          </button>
        )
      })}
    </div>
  )
}

export default AgentNotificationSort
