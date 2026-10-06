import { useMutation, useQueryClient } from '@tanstack/react-query'
import { toast } from 'sonner'
import {
  markAdminNotificationAsRead,
  markAllAdminNotificationsAsRead,
} from '../../api/admin/notifications'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Admin notification writes: mark one read, mark every unread read.
 *
 * BACKING ROUTES (the shared authenticated controller, NOT /admin/notifications,
 * which does not exist):
 *   PATCH /api/v1/notifications/{id}/read
 *   PATCH /api/v1/notifications/read-all
 *
 * WHY INVALIDATION IS A PREFIX INVALIDATE. The list lives under
 * ['admin','notifications',{page,pageSize,sortBy,sortDir}] and every one of those
 * four values is part of the key, so there is no single key to target. The
 * prefix ['admin','notifications'] matches all of them, which is what we want:
 * marking a row read can move it out of an "Unread first" ordering and can empty
 * the final page, so the current page AND the current sort are both potentially
 * stale afterwards. Refetching under the parameters the user is actually
 * looking at preserves their sort and page, which a manual param rewrite or a
 * client-side splice of the cached array would not.
 *
 * WHY NOTHING HERE IS OPTIMISTIC. The list is the only source of truth for both
 * ordering and the page-scoped unread count, and the server is what decides the
 * new order. Painting the row as read before the server has agreed would put a
 * row in a position the server may not agree with, under an ordering whose whole
 * purpose is to be the server's.
 *
 * MARK-ALL AND updatedCount. MarkAllNotificationsAsReadCommandHandler queries
 * `user_id = @UserId AND is_read = false` across every page and returns how many
 * rows it actually flipped, or 0 when there was nothing unread. That number is a
 * RESULT, not a prior total: it is neither a global unread count nor
 * page-scoped, and it is used in the success toast only. It is never rendered as
 * a badge. The button is deliberately not gated on the current page's unread
 * count, because the action it triggers is not page-scoped.
 *
 * A 404 FROM MARK-READ MEANS THE ROW IS GONE. The handler throws
 * KeyNotFound both when the id does not exist and when it belongs to another
 * user, so a 404 is a stale client rather than a server fault. The list is
 * invalidated so the UI drops the row on its own, and the toast says so plainly
 * instead of implying the mark succeeded.
 *
 * Errors are surfaced twice on purpose: a toast, which is the existing Admin
 * convention for a write result, and the mutation error object itself, which the
 * page renders as an inline role="alert" banner. A failed write must never be
 * silent, and must never look like a success.
 */
export function useAdminNotificationMutations() {
  const queryClient = useQueryClient()

  const markRead = useMutation({
    mutationFn: (notificationId) => markAdminNotificationAsRead(notificationId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'notifications'] })
      toast.success('Notification marked as read.')
    },
    onError: (error) => {
      if (error?.response?.status === 404) {
        queryClient.invalidateQueries({ queryKey: ['admin', 'notifications'] })
        toast.error('That notification is no longer available. The list has been refreshed.')
        return
      }
      toast.error(
        extractApiErrorMessage(error, 'Could not mark the notification as read.'),
      )
    },
  })

  const markAllRead = useMutation({
    mutationFn: () => markAllAdminNotificationsAsRead(),
    onSuccess: (result) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'notifications'] })

      const updatedCount = result?.updatedCount ?? 0
      if (updatedCount > 0) {
        toast.success(
          `${updatedCount} notification${updatedCount === 1 ? '' : 's'} marked as read.`,
        )
      } else {
        toast.success('No unread notifications to mark as read.')
      }
    },
    onError: (error) => {
      toast.error(
        extractApiErrorMessage(error, 'Could not mark notifications as read.'),
      )
    },
  })

  return { markRead, markAllRead }
}
