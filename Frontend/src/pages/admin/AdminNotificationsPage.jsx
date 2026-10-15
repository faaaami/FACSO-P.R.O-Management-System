import { useState } from 'react'
import { AlertCircle, Bell, CheckCheck, ShieldCheck } from 'lucide-react'
import AdminPageHeader from '../../components/admin/AdminPageHeader'
import AdminNotificationSort from '../../components/admin/notifications/AdminNotificationSort'
import SectionCard from '../../components/client/SectionCard'
import NotificationsList from '../../components/client/notifications/NotificationsList'
import { useAdminNotifications } from '../../hooks/admin/useAdminNotifications'
import { useAdminNotificationMutations } from '../../hooks/admin/useAdminNotificationMutations'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin Portal notifications.
 *
 * BACKING ROUTES
 *   GET   /api/v1/notifications?page&pageSize&sortBy&sortDir
 *   PATCH /api/v1/notifications/{id}/read
 *   PATCH /api/v1/notifications/read-all
 *
 * THERE IS NO /api/v1/admin/notifications. The Admin portal uses the shared
 * `[Authorize]` controller, the same one the Client and Agent portals have their
 * own role-scoped wrappers for. Every handler resolves the caller's UserId and
 * filters on it, so this page is "notifications addressed to the signed-in Admin
 * account" and nothing wider. It is NOT an organization-wide activity feed, and
 * the copy never implies one — an Admin sees their own inbox, which is the only
 * thing this contract can honestly deliver.
 *
 * WHY THIS PAGE CAN HOLD REAL CONTENT. The backend does generate Admin
 * notifications: when a Client submits a service request, every active Admin
 * account receives one, so the list fills as soon as that work arrives. An
 * earlier revision of this comment claimed the opposite — that every producer
 * targets only a Client contact or an assigned Agent, and that no notification
 * is ever generated for an Admin. That was wrong, and `ServiceRequestDecisionTests`
 * covers the real behaviour. The empty state below is therefore only the normal
 * state while nothing has arrived, never a permanent one.
 *
 * WHY THE UNREAD BADGE IS PAGE-SCOPED AND SUPPRESSED WHILE LOADING OR AFTER A
 * FAILURE. No Admin unread-count endpoint exists — the only one in the backend
 * is restricted to the Client role — so the count is derived from the rows
 * currently loaded. It is labelled "N unread on this page" rather than "N
 * unread", because a page-scoped number wearing a global label is a false
 * measurement. It also falls to 0 while loading and on error, and printing a
 * confident "0" in exactly those two states is the failure mode the Staff page
 * already guards against.
 *
 * WHY "MARK ALL AS READ" IS NOT PAGE-SCOPED. The endpoint marks every unread
 * notification belonging to the signed-in Admin, across all pages, so gating the
 * button on this page's unread count would hide an action that still has work to
 * do — an Admin sitting on a fully-read page 1 would lose access to a mark-all
 * that genuinely needed to run. The button therefore shows whenever the page is
 * available and disables only while the mutation is in flight. The server's
 * updatedCount decides what actually happened, and it is reported in the toast
 * by the mutation hook, never as a badge.
 *
 * WHY SORTING RESETS TO PAGE 1. A different sort is a different result set, so
 * the previous page number is meaningless against it and can point past its end.
 * No client-side sorting happens anywhere: the server owns the order, including
 * the page-scoped behaviour of "Unread first".
 *
 * The list's loading, error and empty branching is not duplicated here.
 * NotificationsList owns it, and receives its empty copy as optional props so
 * Client and Agent keep their own existing strings.
 */
const PAGE_SIZE = 20
const DEFAULT_SORT = 'createdAt:desc'

const ADMIN_PORTAL_BADGE = (
  <span className="inline-flex items-center gap-1.5 rounded-[6px] border border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] px-2.5 py-0.5 text-xs font-semibold text-[#0F9D74]">
    <ShieldCheck size={13} strokeWidth={2} aria-hidden="true" />
    Admin Portal
  </span>
)

/** 'createdAt:desc' -> { sortBy: 'createdAt', sortDir: 'desc' } */
function parseSort(sort) {
  const [sortBy, sortDir] = sort.split(':')
  return { sortBy, sortDir }
}

function AdminNotificationsPage() {
  const [page, setPage] = useState(1)
  const [sort, setSort] = useState(DEFAULT_SORT)

  // `error` is deliberately not destructured: the list error branch is owned by
  // NotificationsList, which renders its own "Could not load notifications."
  // message with a retry. The extracted-message convention below applies to the
  // mutations, which this page does own.
  const { data, unreadCount, isLoading, isError, refresh } =
    useAdminNotifications({ page, pageSize: PAGE_SIZE, ...parseSort(sort) })

  const { markRead, markAllRead } = useAdminNotificationMutations()

  const isMutating = markRead.isPending || markAllRead.isPending
  const mutationFailed = markRead.isError || markAllRead.isError
  const mutationErrorMessage = extractApiErrorMessage(
    markRead.error || markAllRead.error,
    'Could not update notifications.',
  )

  // Both isLoading and isError are load-bearing: totalCount and unreadCount both
  // fall back to 0 in those states, so a badge built on either would report a
  // fabricated zero.
  const showUnreadBadge = !isLoading && !isError && unreadCount > 0

  function handleSortChange(nextSort) {
    setSort(nextSort)
    setPage(1)
  }

  function handlePageChange(nextPage) {
    setPage(nextPage)
  }

  function handleMarkRead(notificationId) {
    markRead.mutate(notificationId)
  }

  function handleMarkAllRead() {
    markAllRead.mutate()
  }

  return (
    <div className="space-y-6">
      <AdminPageHeader
        title="Notifications"
        subtitle="Notifications addressed to your Admin account. The backend does generate Admin notifications — a submitted client service request notifies every active Admin — so this list fills when work arrives."
        action={ADMIN_PORTAL_BADGE}
      />

      <SectionCard
        title="Notifications"
        icon={Bell}
        subtitle="Alerts and updates delivered to this Admin account"
        badge={
          showUnreadBadge ? (
            <span className="rounded-[6px] border border-[#D97706]/20 bg-amber-50 px-2 py-0.5 text-xs font-semibold text-[#D97706]">
              {unreadCount} unread on this page
            </span>
          ) : null
        }
        action={
          <button
            type="button"
            onClick={handleMarkAllRead}
            disabled={isMutating}
            className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 disabled:cursor-not-allowed disabled:opacity-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
          >
            <CheckCheck size={13} strokeWidth={2} aria-hidden="true" />
            Mark all as read
          </button>
        }
      >
        <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <p className="text-xs text-[#6B7280]">Sort notifications</p>
          <AdminNotificationSort
            value={sort}
            onChange={handleSortChange}
            disabled={isMutating}
          />
        </div>

        {mutationFailed && (
          <div
            role="alert"
            className="mb-4 flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
          >
            <AlertCircle
              size={14}
              strokeWidth={2}
              className="mt-0.5 shrink-0 text-[#DC2626]"
              aria-hidden="true"
            />
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
          onPageChange={handlePageChange}
          onMarkRead={handleMarkRead}
          isMutating={isMutating}
          emptyMessage="No notifications yet."
          emptyDescription="Notifications addressed to your Admin account will appear here. The backend does generate Admin notifications — a submitted client service request notifies every active Admin — so this list fills when work arrives."
        />
      </SectionCard>
    </div>
  )
}

export default AdminNotificationsPage
