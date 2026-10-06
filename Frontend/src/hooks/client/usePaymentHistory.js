import { useQuery } from '@tanstack/react-query'
import { getPaymentHistory } from '../../api/client/dashboard'

export function usePaymentHistory(params = {}) {
  return useQuery({
    queryKey: ['client', 'payment-history', params],
    queryFn: () => getPaymentHistory(params),
    staleTime: 30_000,
  })
}