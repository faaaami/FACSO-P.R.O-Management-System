import { useQuery } from '@tanstack/react-query'
import { getAdminNotifications } from '../../api/admin/notifications'

/**
 * Paginated notification list for the signed-in Admin.
 * Backing query GET /api/v1/notifications (shared `[Authorize]` route; the
 * backend scopes rows to the caller's own user id).
 *
 * Params: { page = 1, pageSize = 20, sortBy?, sortDir? }
 * `sortBy` is whitelisted server-side to "title" | "isread" | created_at; any
 * other value silently falls back to created_at ordering, so the UI should only
 * offer the supported options. "isread" (no separator) sorts by read state,
 * which is how "unread first" is achieved — there is no unreadOnly filter.
 * Note the validator only accepts the camelCase token: "is_read" is rejected
 * with a 400 before the repository runs.
 *
 * `unreadCount` IS PAGE-SCOPED AND MUST BE PRESENTED AS SUCH. It counts the
 * unread rows of the page currently loaded and nothing else. The only
 * unread-count route in the backend is GET /api/v1/client/notifications/
 * unread-count, which is `[Authorize(Roles = "Client")]`, so neither Admin nor
 * Agent has a count endpoint to call and both derive this the same way. There is
 * no global unread total available to this page and none is invented: the label
 * reads "N unread on this page" for exactly that reason. The count also falls to
 * 0 while loading and after a failure, so it must not be shown in those states.
 *
 * Writes live in useAdminNotificationMutations.
 */
export function useAdminNotifications(params = {}) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const sortBy = params?.sortBy
  const sortDir = params?.sortDir

  const query = useQuery({
    queryKey: ['admin', 'notifications', { page, pageSize, sortBy, sortDir }],
    queryFn: () => getAdminNotifications({ page, pageSize, sortBy, sortDir }),
    staleTime: 30_000,
    retry: 1,
  })

  const items = query.data?.items ?? []

  return {
    data: query.data,
    items,
    totalCount: query.data?.totalCount ?? 0,
    unreadCount: items.filter((notification) => !notification.isRead).length,
    loading: query.isLoading,
    isLoading: query.isLoading,
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
