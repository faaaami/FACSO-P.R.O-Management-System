import { useState } from 'react'
import { Link } from 'react-router-dom'
import { resetPassword } from '../../api/auth'
import { AuthAlert, AuthLayout, authLinkClass } from '../../components/auth/AuthLayout'
import { useLinkToken } from '../../components/auth/authHelpers'
import SetPasswordForm from './SetPasswordForm'

function ResetPassword() {
  const token = useLinkToken()
  const [done, setDone] = useState(false)

  if (!token) {
    return (
      <AuthLayout title="Reset your password">
        <AuthAlert>This reset link is incomplete. Please request a new one.</AuthAlert>
        <p className="text-center text-sm">
          <Link to="/forgot-password" className={authLinkClass}>Request a new link</Link>
        </p>
      </AuthLayout>
    )
  }

  if (done) {
    return (
      <AuthLayout title="Password updated">
        <AuthAlert tone="success">Your password has been reset. You can now sign in.</AuthAlert>
        <p className="text-center text-sm">
          <Link to="/login" className={authLinkClass}>Go to sign in</Link>
        </p>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout title="Choose a new password" subtitle="This link works once and expires 1 hour after it was sent.">
      <SetPasswordForm
        submitLabel="Reset password"
        busyLabel="Saving..."
        fallbackError="Unable to reset your password. Please request a new link."
        onSubmit={async (newPassword) => {
          await resetPassword({ token, newPassword })
          setDone(true)
        }}
      />
      <p className="mt-4 text-center text-sm text-[#6B7280]">
        Link expired or already used?{' '}
        <Link to="/forgot-password" className={authLinkClass}>Request a new one</Link>
      </p>
    </AuthLayout>
  )
}

export default ResetPassword
