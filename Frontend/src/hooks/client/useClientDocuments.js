import { useQuery } from '@tanstack/react-query'
import { getClientDocuments } from '../../api/client/documents'

export function useClientDocuments(params = {}) {
  return useQuery({
    queryKey: ['client', 'documents', params],
    queryFn: () => getClientDocuments(params),
    staleTime: 30_000,
  })
}