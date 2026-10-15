import { Check } from 'lucide-react'
import StatusPill from '../StatusPill'
import { formatDateTime } from '../billing/format'

function NotificationItem({ notification, onMarkRead, isMutating }) {
  const unread = !notification.isRead

  return (
    <li className="flex flex-col gap-3 rounded-[10px] bg-white border border-[#E2E4E9] p-4 shadow-[0_1px_2px_rgba(28,31,38,0.04)] sm:flex-row sm:items-start sm:justify-between">
      <div className="flex items-start gap-3 min-w-0">
        <span
          className={`mt-1.5 h-2 w-2 shrink-0 rounded-full ${
            unread ? 'bg-[#0F9D74]' : 'border border-[#E2E4E9] bg-[#F7F8FA]'
          }`}
          aria-hidden="true"
        />
        <div className="min-w-0">
          <div className="flex items-center gap-2 flex-wrap">
            <p
              className={`text-sm tracking-tight ${
                unread ? 'font-semibold text-[#16181D]' : 'font-medium text-[#6B7280]'
              }`}
            >
              {notification.title}
            </p>
            <StatusPill label={unread ? 'Unread' : 'Read'} tone={unread ? 'warning' : 'neutral'} />
          </div>
          {notification.message && (
            <p className="mt-1 text-[13px] leading-snug text-[#6B7280]">{notification.message}</p>
          )}
          <p className="mt-1.5 text-xs font-medium text-[#9CA3AF]" data-testid="notification-date">
            {formatDateTime(notification.createdAt) ?? 'N/A'}
          </p>
        </div>
      </div>

      {unread && (
        <button
          type="button"
          onClick={() => onMarkRead(notification.id)}
          disabled={isMutating}
          className="inline-flex shrink-0 items-center gap-1.5 self-start rounded-[10px] bg-white border border-[#E2E4E9] px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer sm:self-center"
        >
          <Check size={14} strokeWidth={2} aria-hidden="true" />
          Mark as read
        </button>
      )}
    </li>
  )
}

export default NotificationItem