import { useState } from 'react'
import { Link } from 'react-router-dom'
import { acceptInvitation } from '../../api/auth'
import { AuthAlert, AuthLayout, authLinkClass } from '../../components/auth/AuthLayout'
import { useLinkToken } from '../../components/auth/authHelpers'
import SetPasswordForm from './SetPasswordForm'

function AcceptInvitation() {
  const token = useLinkToken()
  const [result, setResult] = useState(null)

  if (!token) {
    return (
      <AuthLayout title="Activate your account">
        <AuthAlert>
          This invitation link is incomplete. Please ask your FASCO contact to resend the invitation.
        </AuthAlert>
      </AuthLayout>
    )
  }

  if (result) {
    return (
      <AuthLayout title="Your account is ready">
        <AuthAlert tone="success">{result.message}</AuthAlert>
        <p className="text-center text-sm">
          <Link to="/login" className={authLinkClass}>Go to sign in</Link>
        </p>
      </AuthLayout>
    )
  }

  return (
    <AuthLayout
      title="Activate your account"
      subtitle="Choose a password for the FASCO PRO client portal."
    >
      <SetPasswordForm
        submitLabel="Set password"
        busyLabel="Activating..."
        fallbackError="Unable to activate your account. The invitation may have expired; ask FASCO to resend it."
        onSubmit={async (newPassword) => {
          setResult(await acceptInvitation({ token, newPassword }))
        }}
      />
    </AuthLayout>
  )
}

export default AcceptInvitation
