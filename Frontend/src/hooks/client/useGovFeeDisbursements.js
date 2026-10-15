import { useQuery } from '@tanstack/react-query'
import { getGovFeeDisbursements } from '../../api/client/billing'

export function useGovFeeDisbursements(params = {}) {
  return useQuery({
    queryKey: ['client', 'gov-fee-disbursements', params],
    queryFn: () => getGovFeeDisbursements(params),
    staleTime: 30_000,
  })
}