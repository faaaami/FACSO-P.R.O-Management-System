import { useQuery } from '@tanstack/react-query'
import { getMyProfile } from '../../api/client/account'

export function useClientProfile() {
  return useQuery({
    queryKey: ['client', 'me'],
    queryFn: getMyProfile,
    staleTime: 30_000,
  })
}