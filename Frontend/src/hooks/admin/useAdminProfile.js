import { useQuery } from '@tanstack/react-query'
import { getAdminProfile } from '../../api/admin/users'

/**
 * Shared cache key for the signed-in Admin's own profile. Exported so the
 * Settings mutations can write to exactly the key this query reads, rather than
 * repeating the literal and risking a silent mismatch.
 */
export const ADMIN_PROFILE_QUERY_KEY = ['admin', 'profile']

/**
 * Signed-in Admin's own profile, backing the Admin Settings page.
 * Backing query GET /api/v1/users/me (shared `[Authorize]` route)
 *
 * Returns GetMyProfileResponseDto { id, fullName, email, phone, role, isActive,
 * emailVerified, clientCompanyId }. Only fullName and phone are updatable.
 *
 * There is no Admin-side user administration: no other-account list, no role
 * change, no reactivate and no hard delete.
 *
 * MUTATION HOOKS live in src/hooks/admin/useAdminSettingsMutations.js; the
 * 5-minute staleTime here is intentional and is left as-is.
 */
export function useAdminProfile() {
  const query = useQuery({
    queryKey: ADMIN_PROFILE_QUERY_KEY,
    queryFn: getAdminProfile,
    staleTime: 5 * 60_000,
    retry: 1,
  })

  return {
    data: query.data,
    loading: query.isLoading,
    isLoading: query.isLoading,
    // Exposed so the page's refresh control can disable itself and show a
    // spinner while a manual refetch is in flight. It does not replace
    // isLoading: a background refetch must keep the rendered profile on screen.
    isFetching: query.isFetching,
    error: query.error,
    isError: query.isError,
    refresh: query.refetch,
  }
}
