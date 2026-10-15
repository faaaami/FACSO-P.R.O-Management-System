import { useQuery } from '@tanstack/react-query'
import { getRetainerInvoices } from '../../api/client/billing'

export function useRetainerInvoices(params = {}) {
  return useQuery({
    queryKey: ['client', 'retainer-invoices', params],
    queryFn: () => getRetainerInvoices(params),
    staleTime: 30_000,
  })
}