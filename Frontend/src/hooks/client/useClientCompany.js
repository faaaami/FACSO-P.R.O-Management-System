import { useQuery } from '@tanstack/react-query'
import { getClientCompany } from '../../api/client/dashboard'

export function useClientCompany() {
  return useQuery({
    queryKey: ['client', 'company'],
    queryFn: async () => {
      try {
        return await getClientCompany()
      } catch (error) {
        if (error?.response?.status === 404) return null
        throw error
      }
    },
    staleTime: 30_000,
  })
}