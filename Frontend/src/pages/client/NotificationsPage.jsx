import { useState } from 'react'
import { AlertCircle, Bell, Calendar, CheckCheck } from 'lucide-react'
import { format } from 'date-fns'
import SectionCard from '../../components/client/SectionCard'
import NotificationsList from '../../components/client/notifications/NotificationsList'
import { useClientNotifications } from '../../hooks/client/useClientNotifications'
import { useUnreadNotificationCount } from '../../hooks/client/useUnreadNotificationCount'
import { useMarkNotificationAsRead } from '../../hooks/client/useMarkNotificationAsRead'
import { useMarkAllNotificationsAsRead } from '../../hooks/client/useMarkAllNotificationsAsRead'

const PAGE_SIZE = 10

function NotificationsPage() {
  const [page, setPage] = useState(1)

  const {
    data,
    isLoading,
    isError,
    refetch,
  } = useClientNotifications({ page, pageSize: PAGE_SIZE })

  const { data: unreadCountData } = useUnreadNotificationCount()
  const unreadCount = unreadCountData ?? 0

  const markRead = useMarkNotificationAsRead()
  const markAll = useMarkAllNotificationsAsRead()

  const isMutating = markRead.isPending || markAll.isPending
  const mutationError = markRead.isError || markAll.isError

  const today = format(new Date(), 'EEEE, dd MMMM yyyy')

  return (
    <div className="space-y-6">
      {/* Page header */}
      <header className="flex flex-col gap-4 pb-2 border-b border-[#E2E4E9] md:flex-row md:items-end md:justify-between">
        <div>
          <div className="flex items-center gap-2.5">
            <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">Notifications</h1>
            <span className="inline-flex items-center gap-1.5 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              <Bell size={12} strokeWidth={2} aria-hidden="true" />
              Client Portal
            </span>
          </div>
          <p className="mt-1.5 text-sm text-[#6B7280]">
            Compliance alerts, updates and reminders for your company.
          </p>
        </div>
        <div className="flex items-center gap-3 text-xs text-[#6B7280] shrink-0">
          <div className="flex items-center gap-1.5 rounded-[8px] bg-white border border-[#E2E4E9] px-3 py-1.5 shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
            <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
            <span className="font-medium text-[#16181D]">{today}</span>
          </div>
        </div>
      </header>

      {/* Notifications card */}
      <SectionCard
        title="Notifications"
        icon={Bell}
        subtitle="Alerts and updates from the PRO team"
        badge={
          unreadCount > 0 && (
            <span className="rounded-[6px] bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706] border border-[#D97706]/20">
              {unreadCount} unread
            </span>
          )
        }
        action={
          unreadCount > 0 && (
            <button
              type="button"
              onClick={() => markAll.mutate()}
              disabled={isMutating}
              className="inline-flex items-center gap-1.5 rounded-[10px] bg-white border border-[#E2E4E9] px-3 py-1.5 text-xs font-semibold text-[#16181D] hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
            >
              <CheckCheck size={13} strokeWidth={2} aria-hidden="true" />
              Mark all as read
            </button>
          )
        }
      >
        {mutationError && (
          <div className="mb-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5">
            <AlertCircle size={14} strokeWidth={2} className="text-[#DC2626]" aria-hidden="true" />
            <p className="text-xs font-medium text-[#DC2626]">
              Could not update notification. Please try again.
            </p>
          </div>
        )}

        <NotificationsList
          data={data}
          isLoading={isLoading}
          isError={isError}
          onRetry={() => refetch()}
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          onMarkRead={(id) => markRead.mutate(id)}
          isMutating={isMutating}
        />
      </SectionCard>
    </div>
  )
}

export default NotificationsPage