import { useQuery } from '@tanstack/react-query'
import { getClientServiceRequests } from '../../api/client/serviceRequests'

export function useClientServiceRequests(params = {}) {
  return useQuery({
    queryKey: ['client', 'service-requests', params],
    queryFn: () => getClientServiceRequests(params),
    staleTime: 30_000,
  })
}