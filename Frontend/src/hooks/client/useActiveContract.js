import { useQuery } from '@tanstack/react-query'
import { getActiveContract } from '../../api/client/dashboard'

export function useActiveContract() {
  return useQuery({
    queryKey: ['client', 'active-contract'],
    queryFn: async () => {
      try {
        return await getActiveContract()
      } catch (error) {
        if (error?.response?.status === 404) return null
        throw error
      }
    },
    staleTime: 30_000,
  })
}