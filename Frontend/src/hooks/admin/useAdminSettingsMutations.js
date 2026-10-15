import { useMutation, useQueryClient } from '@tanstack/react-query'
import { useAuth } from '../../auth/AuthContext'
import { changeAdminPassword, updateAdminProfile } from '../../api/admin/users'
import { ADMIN_PROFILE_QUERY_KEY } from './useAdminProfile'

/**
 * PATCH /api/v1/users/me
 * Body { fullName, phone } — the only two updatable fields on the user.
 *
 * Responds with UpdateMyProfileResponseDto, which is property-for-property
 * identical to GetMyProfileResponseDto, so the response can be written straight
 * into the profile cache instead of triggering a second round trip. Phone is
 * normalised server-side: a blank value is stored as null, which is how a user
 * clears it.
 *
 * This endpoint writes NO audit row (unlike change-password), so callers must not
 * describe a profile edit as recorded or audited.
 */
export function useUpdateAdminProfile() {
  const queryClient = useQueryClient()
  const { updateUser } = useAuth()

  return useMutation({
    mutationFn: updateAdminProfile,
    onSuccess: (profile, variables) => {
      if (profile) {
        queryClient.setQueryData(ADMIN_PROFILE_QUERY_KEY, profile)
      } else {
        queryClient.invalidateQueries({ queryKey: ADMIN_PROFILE_QUERY_KEY })
      }

      // Refresh the cached session so the header stops showing a stale name.
      // updateUser merges into the stored user, so only fields we actually know
      // may be included - an undefined value would wipe them out. role, email and
      // the tokens are immutable here and are never part of this patch.
      const patch = {}
      const fullName = profile?.fullName ?? variables?.fullName
      if (fullName != null) {
        patch.fullName = fullName
      }
      const phone = profile?.phone ?? variables?.phone
      if (phone !== undefined) {
        patch.phone = phone
      }
      if (Object.keys(patch).length > 0) {
        updateUser(patch)
      }
    },
  })
}

/**
 * POST /api/v1/users/change-password
 * Body { currentPassword, newPassword, confirmPassword }. Returns a short
 * confirmation string, not a profile.
 *
 * The backend verifies the current password (409 on mismatch), enforces a
 * length-only minimum of 8 characters, and writes a PasswordChanged audit row.
 * It does NOT revoke the refresh token, invalidate the access token or sign the
 * user out, so success copy must not claim any of that. There is deliberately no
 * cache or session write-through here: nothing about the session changed.
 *
 * Messaging and form reset are owned by the calling form, mirroring
 * useChangeAgentPassword.
 */
export function useChangeAdminPassword() {
  return useMutation({
    mutationFn: changeAdminPassword,
  })
}
