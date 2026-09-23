import { useQuery } from '@tanstack/react-query'
import { getClientDocumentScan } from '../../api/client/documents'

export function useClientDocumentScan(id) {
  return useQuery({
    queryKey: ['client', 'document', id, 'scan'],
    queryFn: () => getClientDocumentScan(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}