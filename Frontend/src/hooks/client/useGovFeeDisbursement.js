import { useQuery } from '@tanstack/react-query'
import { getGovFeeDisbursement } from '../../api/client/billing'

export function useGovFeeDisbursement(id) {
  return useQuery({
    queryKey: ['client', 'gov-fee-disbursement', id],
    queryFn: () => getGovFeeDisbursement(id),
    enabled: Boolean(id),
    staleTime: 30_000,
  })
}