import { useQuery } from '@tanstack/react-query'
import { getClientServiceRequest } from '../../api/client/serviceRequests'

export function useClientServiceRequest(id) {
  return useQuery({
    queryKey: ['client', 'service-request', id],
    queryFn: () => getClientServiceRequest(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}