import { useQuery } from '@tanstack/react-query'
import { getClientDocument } from '../../api/client/documents'

export function useClientDocument(id) {
  return useQuery({
    queryKey: ['client', 'document', id],
    queryFn: () => getClientDocument(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}