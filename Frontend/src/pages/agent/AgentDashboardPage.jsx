import { useState } from 'react'
import { Bell } from 'lucide-react'
import { toast } from 'sonner'
import SectionCard from '../../components/client/SectionCard'
import LoadingState from '../../components/client/LoadingState'
import ErrorState from '../../components/client/ErrorState'
import EmptyState from '../../components/client/EmptyState'
import AgentPageHeader from '../../components/agent/common/AgentPageHeader'
import DashboardKpiRow from '../../components/agent/dashboard/DashboardKpiRow'
import DashboardAttentionNeeded from '../../components/agent/dashboard/DashboardAttentionNeeded'
import DashboardWorkQueue from '../../components/agent/dashboard/DashboardWorkQueue'
import DashboardExpiringPreview from '../../components/agent/dashboard/DashboardExpiringPreview'
import AgentTaskDetailDrawer from '../../components/agent/renewalTasks/AgentTaskDetailDrawer'
import { useAgentWorkload } from '../../hooks/agent/useAgentWorkload'
import { useAgentNotifications } from '../../hooks/agent/useAgentNotifications'
import { useAgentExpiringDocuments } from '../../hooks/agent/useAgentExpiringDocuments'
import { useAgentProfile } from '../../hooks/agent/useAgentProfile'
import { extractApiErrorMessage } from '../../utils/apiError'

const PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <span className="h-1.5 w-1.5 rounded-full bg-[#0F9D74]" aria-hidden="true" />
    Agent Portal
  </span>
)

function greetingFor(name) {
  const hour = new Date().getHours()
  const part = hour < 12 ? 'Good morning' : hour < 18 ? 'Good afternoon' : 'Good evening'
  if (!name) return `${part}`
  return `${part}, ${name}`
}

function firstNameOf(fullName) {
  if (!fullName) return null
  const trimmed = String(fullName).trim()
  if (!trimmed) return null
  return trimmed.split(/\s+/)[0]
}

function DashboardGreeting() {
  const { data: profile } = useAgentProfile()
  const firstName = firstNameOf(profile?.fullName)
  const today = new Date().toLocaleDateString('en-GB', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric',
  })

  return (
    <AgentPageHeader
      title={greetingFor(firstName)}
      badge={PORTAL_BADGE}
      description="Manage your assigned renewal tasks, client workload, and compliance pipeline."
      meta={<p className="text-xs text-[#9CA3AF]">{today}</p>}
    />
  )
}

function NotificationsPreviewCard() {
  const { items, unreadCount, isLoading, isError, refresh, markRead, markAllRead } =
    useAgentNotifications({ page: 1, pageSize: 10 })

  const mutationFailed = markRead.isError || markAllRead.isError
  const mutationErrorMessage = extractApiErrorMessage(
    markRead.error || markAllRead.error,
    'Could not update notifications.',
  )

  function handleMarkAllRead() {
    markAllRead.mutate(undefined, {
      onSuccess: () => toast.success('Notifications marked as read.'),
      onError: (error) =>
        toast.error(extractApiErrorMessage(error, 'Could not mark notifications as read.')),
    })
  }

  function handleMarkRead(notificationId) {
    markRead.mutate(notificationId, {
      onError: (error) =>
        toast.error(extractApiErrorMessage(error, 'Could not mark the notification as read.')),
    })
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading notifications…" />
  } else if (isError) {
    content = <ErrorState message="Could not load notifications." onRetry={() => refresh()} />
  } else if (items.length === 0) {
    content = (
      <EmptyState
        icon={Bell}
        message="No notifications yet."
        description="System alerts and task updates will appear here."
      />
    )
  } else {
    content = (
      <>
        {mutationFailed && (
          <div className="mb-3 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5">
            <p className="text-xs font-medium text-[#DC2626]">{mutationErrorMessage}</p>
          </div>
        )}
        <div className="flex items-center justify-between gap-2">
          <div className="flex items-baseline gap-2">
            <span className="text-3xl font-bold tracking-tight text-[#16181D]">{unreadCount}</span>
            <span className="text-xs text-[#6B7280]">
              {unreadCount === 1
                ? 'unread notification on this page'
                : 'unread notifications on this page'}
            </span>
          </div>
          <button
            type="button"
            onClick={handleMarkAllRead}
            disabled={markAllRead.isPending || unreadCount === 0}
            className="rounded-[6px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
          >
            Mark all read
          </button>
        </div>

        <ul className="mt-3 divide-y divide-[#E2E4E9]">
          {items.slice(0, 5).map((notification) => (
            <li
              key={notification.id}
              className={`flex flex-col gap-1 py-2.5 ${notification.isRead ? 'opacity-70' : ''}`}
            >
              <div className="flex items-start justify-between gap-2">
                <p className="flex items-center gap-2 text-sm font-semibold text-[#16181D]">
                  {!notification.isRead && (
                    <span className="h-1.5 w-1.5 rounded-full bg-[#D97706]" aria-hidden="true" />
                  )}
                  {notification.title ?? 'Notification'}
                </p>
                {!notification.isRead && (
                  <button
                    type="button"
                    onClick={() => handleMarkRead(notification.id)}
                    disabled={markRead.isPending}
                    className="shrink-0 rounded-[6px] px-1.5 py-0.5 text-xs font-semibold text-[#0F9D74] transition duration-150 hover:text-[#0B7A5B] disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                  >
                    Mark read
                  </button>
                )}
              </div>
              {notification.message && (
                <p className="line-clamp-2 text-xs text-[#6B7280]">{notification.message}</p>
              )}
            </li>
          ))}
        </ul>
      </>
    )
  }

  return (
    <SectionCard
      title="Notifications"
      icon={Bell}
      subtitle="Alerts & updates from your renewal workflow"
      badge={
        unreadCount > 0 && (
          <span className="rounded-[6px] border border-[#D97706]/20 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706]">
            {unreadCount} unread on this page
          </span>
        )
      }
    >
      {content}
    </SectionCard>
  )
}

function AgentDashboardPage() {
  const [openTaskId, setOpenTaskId] = useState(null)

  const { data: workload } = useAgentWorkload()
  const { unreadCount } = useAgentNotifications({ page: 1, pageSize: 10 })
  const { totalCount: expiringCount } = useAgentExpiringDocuments({
    days: 30,
    page: 1,
    pageSize: 5,
    includeExpired: false,
  })

  const activeTaskCount = workload?.activeTaskCount ?? 0
  const blockedTaskCount = workload?.blockedTaskCount ?? 0

  return (
    <div className="space-y-6">
      <DashboardGreeting />
      <DashboardKpiRow
        activeTasks={activeTaskCount}
        blockedTasks={blockedTaskCount}
        expiringSoon={expiringCount}
        unreadNotifications={unreadCount}
      />

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-12">
        <div className="space-y-6 lg:col-span-7">
          <DashboardWorkQueue onOpenTask={setOpenTaskId} />
          <DashboardAttentionNeeded onOpenTask={setOpenTaskId} />
        </div>
        <div className="space-y-6 lg:col-span-5">
          <DashboardExpiringPreview />
          <NotificationsPreviewCard />
        </div>
      </div>

      {openTaskId && (
        <AgentTaskDetailDrawer taskId={openTaskId} onClose={() => setOpenTaskId(null)} />
      )}
    </div>
  )
}

export default AgentDashboardPage
