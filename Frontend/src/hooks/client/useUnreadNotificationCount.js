import { useQuery } from '@tanstack/react-query'
import { getUnreadNotificationCount } from '../../api/client/dashboard'

export function useUnreadNotificationCount() {
  return useQuery({
    queryKey: ['client', 'notifications', 'unread-count'],
    queryFn: getUnreadNotificationCount,
    staleTime: 30_000,
  })
}