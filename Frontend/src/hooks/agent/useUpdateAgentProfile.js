import { useMutation, useQueryClient } from '@tanstack/react-query'
import { updateAgentMe } from '../../api/agent/me'
import { useAuth } from '../../auth/AuthContext'

/**
 * PATCH /api/v1/agent/me -> UpdateMyProfileResponseDto { id, fullName, email,
 * phone, role, isActive, emailVerified, clientCompanyId }
 */
export function useUpdateAgentProfile() {
  const queryClient = useQueryClient()
  const { updateUser } = useAuth()

  return useMutation({
    mutationFn: updateAgentMe,
    onSuccess: (profile, variables) => {
      queryClient.invalidateQueries({ queryKey: ['agent', 'me'] })
      // Refresh the cached session so the header and settings views stop showing
      // stale values. updateUser merges into the stored user, so only include
      // fields we actually know - an undefined value would wipe them out.
      const patch = {}
      const fullName = profile?.fullName ?? variables?.fullName
      if (fullName != null) {
        patch.fullName = fullName
      }
      const phone = profile?.phone ?? variables?.phone
      if (phone !== undefined) {
        patch.phone = phone
      }
      updateUser(patch)
    },
  })
}
