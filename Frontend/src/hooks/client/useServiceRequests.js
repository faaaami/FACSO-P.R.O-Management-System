import { useQuery } from '@tanstack/react-query'
import { getServiceRequests } from '../../api/client/dashboard'

export function useServiceRequests(params = {}) {
  return useQuery({
    queryKey: ['client', 'service-requests', params],
    queryFn: () => getServiceRequests(params),
    staleTime: 30_000,
  })
}