import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import {
  getAgentNotifications,
  markAgentNotificationAsRead,
  markAllAgentNotificationsAsRead,
} from '../../api/agent/notifications'

/**
 * Loads the Agent's notifications plus read / read-all handlers.
 * Backing query GET /api/v1/agent/notifications
 *
 * `unreadCount` is derived from the current page only, because the Agent
 * notification endpoints expose no account-wide unread total. Treat it as
 * page-scoped and label it as such in the UI.
 *
 * `markRead` / `markAllRead` are TanStack mutations, so callers can read
 * `isError` / `error` / `isPending` and pass per-call `onError` handlers to
 * surface failures. Mutation errors are never rethrown by this hook.
 */
export function useAgentNotifications(params = {}) {
  const queryClient = useQueryClient()
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 20
  const sortBy = params?.sortBy ?? 'createdAt'
  const sortDir = params?.sortDir ?? 'desc'

  const query = useQuery({
    queryKey: ['agent', 'notifications', { page, pageSize, sortBy, sortDir }],
    queryFn: () => getAgentNotifications({ page, pageSize, sortBy, sortDir }),
    staleTime: 30_000,
    retry: 1,
  })

  const markRead = useMutation({
    mutationFn: (notificationId) => markAgentNotificationAsRead(notificationId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent', 'notifications'] }),
  })

  const markAllRead = useMutation({
    mutationFn: () => markAllAgentNotificationsAsRead(),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['agent', 'notifications'] }),
  })

  const items = query.data?.items ?? []

  return {
    data: query.data,
    items,
    totalCount: query.data?.totalCount ?? 0,
    unreadCount: items.filter((n) => !n.isRead).length,
    loading: query.isLoading,
    isLoading: query.isLoading,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
    markRead,
    markAllRead,
  }
}
