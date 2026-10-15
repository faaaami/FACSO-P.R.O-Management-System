import { useQuery } from '@tanstack/react-query'
import { getServiceFeeInvoices } from '../../api/client/billing'

export function useServiceFeeInvoices(params = {}) {
  return useQuery({
    queryKey: ['client', 'service-fee-invoices', params],
    queryFn: () => getServiceFeeInvoices(params),
    staleTime: 30_000,
  })
}