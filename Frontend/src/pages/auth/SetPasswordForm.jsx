import { useState } from 'react'
import { Loader2 } from 'lucide-react'
import { extractApiErrorMessage } from '../../utils/apiError'
import {
  AuthAlert,
  PasswordInput,
  authButtonClass,
} from '../../components/auth/AuthLayout'
import { validateNewPassword } from '../../components/auth/authHelpers'

/**
 * New password + confirmation, shared by the reset-password and
 * accept-invitation pages. `onSubmit(password)` performs the request.
 */
function SetPasswordForm({ submitLabel, busyLabel, onSubmit, fallbackError }) {
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  async function handleSubmit(event) {
    event.preventDefault()
    if (isSubmitting) return

    const problem = validateNewPassword(password, confirmation)
    if (problem) {
      setError(problem)
      return
    }

    setError(null)
    setIsSubmitting(true)
    try {
      await onSubmit(password)
    } catch (err) {
      setError(extractApiErrorMessage(err, fallbackError))
      setIsSubmitting(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-4" noValidate>
      {error && <AuthAlert>{error}</AuthAlert>}

      <PasswordInput
        id="new-password"
        label="New password"
        value={password}
        onChange={setPassword}
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
            <span>{busyLabel}</span>
          </>
        ) : (
          <span>{submitLabel}</span>
        )}
      </button>
    </form>
  )
}

export default SetPasswordForm
