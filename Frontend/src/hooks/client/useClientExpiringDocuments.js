import { useQuery } from '@tanstack/react-query'
import { getClientExpiringDocuments } from '../../api/client/documents'

export function useClientExpiringDocuments(params = {}) {
  return useQuery({
    queryKey: ['client', 'documents', 'expiring', params],
    queryFn: () => getClientExpiringDocuments(params),
    staleTime: 30_000,
  })
}