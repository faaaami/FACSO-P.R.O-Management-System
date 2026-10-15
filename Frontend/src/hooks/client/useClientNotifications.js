import { useQuery } from '@tanstack/react-query'
import { getNotifications } from '../../api/client/notifications'

export function useClientNotifications(params) {
  const page = params?.page ?? 1
  const pageSize = params?.pageSize ?? 10

  return useQuery({
    queryKey: ['client', 'notifications', 'list', pageSize, page],
    queryFn: () => getNotifications(params),
    staleTime: 30_000,
  })
}