import { useState } from 'react'
import { AlertCircle, Bell, Calendar, CheckCheck } from 'lucide-react'
import { format } from 'date-fns'
import { toast } from 'sonner'
import SectionCard from '../../components/client/SectionCard'
import NotificationsList from '../../components/client/notifications/NotificationsList'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import AgentNotificationSort from '../../components/agent/notifications/AgentNotificationSort'
import { useAgentNotifications } from '../../hooks/agent/useAgentNotifications'
import { extractApiErrorMessage } from '../../utils/apiError'

const PAGE_SIZE = 10
const DEFAULT_SORT = 'createdAt:desc'

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

function parseSort(sort) {
  const [sortBy, sortDir] = sort.split(':')
  return { sortBy, sortDir }
}

function AgentNotificationsHeader() {
  return (
    <AgentPageHeader
      title="Notifications"
      badge={PORTAL_BADGE}
      description="Compliance alerts, task updates and reminders for your portfolio."
      meta={
        <div className="flex items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
          <Calendar size={14} className="text-[#6B7280]" aria-hidden="true" />
          <span className="font-medium text-[#16181D]">
            {format(new Date(), 'EEEE, dd MMMM yyyy')}
          </span>
        </div>
      }
    />
  )
}

function AgentNotificationsPage() {
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState(DEFAULT_SORT)

  const {
    data,
    items,
    unreadCount,
    isLoading,
    isError,
    refresh,
    markRead,
    markAllRead,
  } = useAgentNotifications({ page, pageSize: PAGE_SIZE, ...parseSort(sort) })

  const isMutating = markRead.isPending || markAllRead.isPending
  const hasUnreadOnPage = items.length > 0 && unreadCount > 0
  const mutationFailed = markRead.isError || markAllRead.isError
  const mutationErrorMessage = extractApiErrorMessage(
    markRead.error || markAllRead.error,
    'Could not update notifications.',
  )

  function handleSortChange(nextSort) {
    setSort(nextSort)
    setPage(1)
  }

  function handleMarkRead(notificationId) {
    markRead.mutate(notificationId, {
      onSuccess: () => toast.success('Notification marked as read.'),
      onError: (error) =>
        toast.error(extractApiErrorMessage(error, 'Could not mark the notification as read.')),
    })
  }

  function handleMarkAllRead() {
    markAllRead.mutate(undefined, {
      onSuccess: (result) => {
        const updatedCount = result?.updatedCount ?? 0
        if (updatedCount > 0) {
          toast.success(
            `${updatedCount} notification${updatedCount === 1 ? '' : 's'} marked as read.`,
          )
        } else {
          toast.success('No unread notifications to mark as read.')
        }
      },
      onError: (error) =>
        toast.error(extractApiErrorMessage(error, 'Could not mark notifications as read.')),
    })
  }

  return (
    <div className="space-y-6">
      <AgentNotificationsHeader />

      <SectionCard
        title="Notifications"
        icon={Bell}
        subtitle="Alerts and updates from your renewal workflow"
        badge={
          hasUnreadOnPage && (
            <span className="rounded-[6px] bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706] border border-[#D97706]/20">
              {unreadCount} unread on this page
            </span>
          )
        }
        action={
          hasUnreadOnPage && (
            <button
              type="button"
              onClick={handleMarkAllRead}
              disabled={isMutating}
              className="inline-flex items-center gap-1.5 rounded-[10px] bg-white border border-[#E2E4E9] px-3 py-1.5 text-xs font-semibold text-[#16181D] hover:bg-gray-50 disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
            >
              <CheckCheck size={13} strokeWidth={2} aria-hidden="true" />
              Mark all as read
            </button>
          )
        }
      >
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-[#6B7280]">Sort notifications</p>
          <AgentNotificationSort value={sort} onChange={handleSortChange} disabled={isMutating} />
        </div>

        {mutationFailed && (
          <div className="mb-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5">
            <AlertCircle size={14} strokeWidth={2} className="mt-0.5 shrink-0 text-[#DC2626]" aria-hidden="true" />
            <p className="text-xs font-medium text-[#DC2626]">{mutationErrorMessage}</p>
          </div>
        )}

        <NotificationsList
          data={data}
          isLoading={isLoading}
          isError={isError}
          onRetry={() => refresh()}
          page={page}
          pageSize={PAGE_SIZE}
          onPageChange={setPage}
          onMarkRead={handleMarkRead}
          isMutating={isMutating}
        />
      </SectionCard>
    </div>
  )
}

export default AgentNotificationsPage
