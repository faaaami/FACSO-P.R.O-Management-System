import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { Loader2 } from 'lucide-react'
import { verifyEmail } from '../../api/auth'
import { extractApiErrorMessage } from '../../utils/apiError'
import { AuthAlert, AuthLayout, authLinkClass } from '../../components/auth/AuthLayout'
import { useLinkToken } from '../../components/auth/authHelpers'

function VerifyEmail() {
  const token = useLinkToken()
  const [status, setStatus] = useState(token ? 'verifying' : 'missing')
  const [error, setError] = useState(null)
  // The token is single-use: guard against React StrictMode's double effect.
  const submitted = useRef(false)

  useEffect(() => {
    if (!token || submitted.current) return
    submitted.current = true

    verifyEmail(token)
      .then(() => setStatus('verified'))
      .catch((err) => {
        setError(extractApiErrorMessage(err, 'This verification link is invalid or has expired.'))
        setStatus('failed')
      })
  }, [token])

  return (
    <AuthLayout title="Verify your email">
      {status === 'verifying' && (
        <div className="flex items-center justify-center gap-2 text-sm text-[#6B7280]">
          <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
          <span>Verifying...</span>
        </div>
      )}
      {status === 'verified' && (
        <AuthAlert tone="success">Your email address is verified. You can now sign in.</AuthAlert>
      )}
      {status === 'missing' && <AuthAlert>This verification link is incomplete.</AuthAlert>}
      {status === 'failed' && <AuthAlert>{error}</AuthAlert>}

      {status !== 'verifying' && (
        <p className="text-center text-sm text-[#6B7280]">
          <Link to="/login" className={authLinkClass}>Go to sign in</Link>
          {status !== 'verified' && (
            <> &middot; you can request a new link from the sign-in page.</>
          )}
        </p>
      )}
    </AuthLayout>
  )
}

export default VerifyEmail
