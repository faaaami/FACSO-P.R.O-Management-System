import { useQuery } from '@tanstack/react-query'
import { getExpiringDocuments } from '../../api/client/dashboard'

export function useExpiringDocuments(params = {}) {
  return useQuery({
    queryKey: ['client', 'expiring-documents', params],
    queryFn: () => getExpiringDocuments(params),
    staleTime: 30_000,
  })
}