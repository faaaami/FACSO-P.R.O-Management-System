import { useState } from 'react'
import { Link } from 'react-router-dom'
import { Loader2, Mail } from 'lucide-react'
import { requestPasswordReset } from '../../api/auth'
import { extractApiErrorMessage } from '../../utils/apiError'
import {
  AuthAlert,
  AuthLayout,
  authButtonClass,
  authInputClass,
  authLinkClass,
} from '../../components/auth/AuthLayout'

function ForgotPassword() {
  const [email, setEmail] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)
  const [sentMessage, setSentMessage] = useState(null)

  async function handleSubmit(event) {
    event.preventDefault()
    if (isSubmitting) return

    const trimmed = email.trim()
    if (!trimmed) {
      setError('Please enter your email address.')
      return
    }

    setError(null)
    setIsSubmitting(true)
    try {
      const result = await requestPasswordReset(trimmed)
      setSentMessage(result?.message ?? 'If an account exists with this email, a reset link has been sent.')
    } catch (err) {
      setError(extractApiErrorMessage(err, 'Unable to send the reset link. Please try again.'))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <AuthLayout
      title="Forgot your password?"
      subtitle="Enter your email and we will send you a link to choose a new one."
    >
      {error && <AuthAlert>{error}</AuthAlert>}

      {sentMessage ? (
        <>
          <AuthAlert tone="success">
            {sentMessage} The link expires in 1 hour and works once.
          </AuthAlert>
          <p className="text-center text-sm text-[#6B7280]">
            <Link to="/login" className={authLinkClass}>Back to sign in</Link>
          </p>
        </>
      ) : (
        <form onSubmit={handleSubmit} className="space-y-4" noValidate>
          <div>
            <label htmlFor="forgot-email" className="block text-sm font-medium text-[#16181D] mb-1.5">
              Email address
            </label>
            <div className="relative">
              <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#9CA3AF]">
                <Mail className="h-4 w-4" />
              </div>
              <input
                id="forgot-email"
                type="email"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                placeholder="name@company.ae"
                autoComplete="email"
                disabled={isSubmitting}
                required
                className={authInputClass}
              />
            </div>
          </div>

          <button type="submit" disabled={isSubmitting} className={authButtonClass}>
            {isSubmitting ? (
              <>
                <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
                <span>Sending...</span>
              </>
            ) : (
              <span>Send reset link</span>
            )}
          </button>

          <p className="text-center text-sm text-[#6B7280]">
            <Link to="/login" className={authLinkClass}>Back to sign in</Link>
          </p>
        </form>
      )}
    </AuthLayout>
  )
}

export default ForgotPassword
