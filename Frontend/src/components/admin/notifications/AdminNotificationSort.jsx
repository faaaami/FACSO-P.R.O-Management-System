import { ArrowDownWideNarrow, ArrowUpNarrowWide, CircleDot } from 'lucide-react'

/**
 * Sort control for the Admin notification list.
 *
 * THE THREE OPTIONS ARE THE ONLY ONES THE BACKEND HONOURS.
 * GET /api/v1/notifications is whitelisted by GetNotificationsQueryValidator to
 * sortBy in {createdAt, title, isRead} and sortDir in {asc, desc}. The repository
 * then maps "title", "isread" and everything-else to created_at. "title" is
 * genuinely supported and is deliberately NOT offered: three options is the
 * established notification-sorting vocabulary in this product, and a fourth
 * would be one more control for an Admin to read past.
 *
 * "Unread first" is a SORT, not a filter. There is no unreadOnly parameter on
 * this endpoint, so this option still returns read and unread rows together,
 * merely ordered unread-first. It must never be presented as if it narrowed the
 * result set.
 *
 * `isRead` IS SENT, AND THE SPELLING IS LOAD-BEARING. The validator compares
 * OrdinalIgnoreCase against "isRead" and rejects "is_read" with a 400 before the
 * repository ever runs, so the underscore form is not a harmless synonym here.
 *
 * WHY AN ADMIN-LOCAL TWIN RATHER THAN AN IMPORT OF AgentNotificationSort. The
 * three options and their visual treatment match the Agent control exactly, but
 * every Admin module owns its own filter/sort control (AdminStaffFilterBar,
 * AdminDocumentFilterBar, AdminTaskFilterBar, AdminBillingFilterBar), and an
 * Admin page should not take a dependency on a component owned by another portal
 * whose option list is that portal's to change.
 */
const SORT_OPTIONS = [
  { value: 'createdAt:desc', label: 'Newest', Icon: ArrowDownWideNarrow },
  { value: 'createdAt:asc', label: 'Oldest', Icon: ArrowUpNarrowWide },
  { value: 'isRead:asc', label: 'Unread first', Icon: CircleDot },
]

function AdminNotificationSort({ value, onChange, disabled = false }) {
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

export default AdminNotificationSort
