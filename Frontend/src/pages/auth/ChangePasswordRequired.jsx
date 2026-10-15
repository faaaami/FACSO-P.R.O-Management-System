import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { changePassword } from '../../api/auth'
import { useAuth } from '../../auth/AuthContext'
import { extractApiErrorMessage } from '../../utils/apiError'
import {
  AuthAlert,
  AuthLayout,
  PasswordInput,
  authButtonClass,
  authLinkClass,
} from '../../components/auth/AuthLayout'
import { validateNewPassword } from '../../components/auth/authHelpers'

const ROLE_HOME = { Client: '/client', Agent: '/agent', Admin: '/admin' }

/**
 * Shown after signing in with a temporary password (the bootstrap Admin).
 * The API refuses every other request until this succeeds.
 */
function ChangePasswordRequired() {
  const { user, logout, completePasswordChange } = useAuth()
  const navigate = useNavigate()
  const [currentPassword, setCurrentPassword] = useState('')
  const [newPassword, setNewPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  async function handleSubmit(event) {
    event.preventDefault()
    if (isSubmitting) return

    if (!currentPassword) {
      setError('Please enter your current password.')
      return
    }
    const problem =
      validateNewPassword(newPassword, confirmation) ??
      (newPassword === currentPassword ? 'Choose a password different from the current one.' : null)
    if (problem) {
      setError(problem)
      return
    }

    setError(null)
    setIsSubmitting(true)
    try {
      await changePassword({ currentPassword, newPassword, confirmPassword: confirmation })
      const updated = await completePasswordChange()
      navigate(ROLE_HOME[updated?.role] ?? '/', { replace: true })
    } catch (err) {
      setError(extractApiErrorMessage(err, 'Unable to change your password. Please try again.'))
      setIsSubmitting(false)
    }
  }

  async function handleSignOut() {
    await logout()
    navigate('/login', { replace: true })
  }

  return (
    <AuthLayout
      title="Change your password"
      subtitle={`${user?.email ?? 'Your account'} is using a temporary password. Choose your own to continue.`}
    >
      {error && <AuthAlert>{error}</AuthAlert>}

      <form onSubmit={handleSubmit} className="space-y-4" noValidate>
        <PasswordInput
          id="current-password"
          label="Current (temporary) password"
          value={currentPassword}
          onChange={setCurrentPassword}
          disabled={isSubmitting}
          autoComplete="current-password"
        />
        <PasswordInput
          id="new-password"
          label="New password"
          value={newPassword}
          onChange={setNewPassword}
          disabled={isSubmitting}
        />
        <PasswordInput
          id="confirm-password"
          label="Confirm new password"
          value={confirmation}
          onChange={setConfirmation}
          disabled={isSubmitting}
        />
        <p className="text-xs text-[#6B7280]">Use 8 to 100 characters.</p>

        <button type="submit" disabled={isSubmitting} className={authButtonClass}>
          {isSubmitting ? (
            <>
              <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
              <span>Saving...</span>
            </>
          ) : (
            <span>Change password</span>
          )}
        </button>
      </form>

      <p className="mt-4 text-center text-sm">
        <button type="button" onClick={handleSignOut} className={`${authLinkClass} cursor-pointer`}>
          Sign out
        </button>
      </p>
    </AuthLayout>
  )
}

export default ChangePasswordRequired
