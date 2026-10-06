import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import {
  Building2,
  Mail,
  Lock,
  Eye,
  EyeOff,
  AlertCircle,
  Loader2,
  ShieldCheck,
} from 'lucide-react'
import { useAuth } from '../auth/AuthContext'

const ROLE_HOME = {
  Client: '/client',
  Agent: '/agent',
  Admin: '/admin',
}

function resolveRoleDestination(role) {
  return ROLE_HOME[role] ?? '/'
}

function extractErrorMessage(error) {
  if (error?.response?.data?.error?.message) {
    return error.response.data.error.message
  }
  if (error?.message) {
    return error.message
  }
  return 'Unable to sign in. Please check your credentials and try again.'
}

function Login() {
  const { user, isAuthenticated, isInitializing, login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [showPassword, setShowPassword] = useState(false)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  if (isInitializing) {
    return (
      <div className="min-h-screen bg-[#F7F8FA] flex items-center justify-center">
        <div className="flex flex-col items-center gap-3">
          <Loader2 className="w-8 h-8 animate-spin text-[#0F9D74]" />
          <span className="text-sm font-medium text-[#6B7280]">Loading portal...</span>
        </div>
      </div>
    )
  }

  if (isAuthenticated && user) {
    return <Navigate to={resolveRoleDestination(user.role)} replace />
  }

  async function handleSubmit(event) {
    event.preventDefault()

    if (isSubmitting) {
      return
    }

    setError(null)

    const trimmedEmail = email.trim()
    if (!trimmedEmail || !password) {
      setError('Please enter your email and password.')
      return
    }

    setIsSubmitting(true)

    try {
      const data = await login({ email: trimmedEmail, password })
      navigate(resolveRoleDestination(data.data.user.role), { replace: true })
    } catch (err) {
      setError(extractErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <div className="min-h-screen bg-[#F7F8FA] flex flex-col justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        {/* FASCO PRO Brand Header */}
        <div className="flex flex-col items-center text-center mb-8">
          <div className="flex items-center gap-3 mb-2">
            <div className="flex h-11 w-11 items-center justify-center rounded-[10px] bg-[#1C1F26] shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
              <Building2 className="h-5 w-5 text-[#0F9D74]" />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-2xl font-bold tracking-tight text-[#16181D]">
                FASCO
              </span>
              <span className="rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold tracking-wide text-[#0F9D74] border border-[#0F9D74]/20">
                PRO Service
              </span>
            </div>
          </div>
          <p className="text-xs font-medium text-[#6B7280] tracking-wider uppercase">
            UAE Government Relations & Corporate PRO Management
          </p>
        </div>

        {/* Login Card */}
        <div className="bg-white rounded-[12px] border border-[#E2E4E9] shadow-[0_1px_3px_rgba(28,31,38,0.06)] p-7 sm:p-8">
          <div className="mb-6 text-center">
            <h1 className="text-xl font-semibold tracking-tight text-[#16181D]">
              Sign in to your account
            </h1>
            <p className="mt-1.5 text-sm text-[#6B7280]">
              Enter your credentials to access the FASCO PRO management portal
            </p>
          </div>

          {error && (
            <div
              role="alert"
              className="mb-5 flex items-start gap-2.5 rounded-[10px] bg-red-50 p-3 text-sm text-[#DC2626] border border-red-200"
            >
              <AlertCircle className="h-4 w-4 shrink-0 text-[#DC2626] mt-0.5" />
              <div className="leading-snug font-medium text-xs sm:text-sm">{error}</div>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4" noValidate>
            <div>
              <label
                htmlFor="email"
                className="block text-sm font-medium text-[#16181D] mb-1.5"
              >
                Email address
              </label>
              <div className="relative">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#9CA3AF]">
                  <Mail className="h-4 w-4" />
                </div>
                <input
                  id="email"
                  type="email"
                  value={email}
                  onChange={(event) => setEmail(event.target.value)}
                  placeholder="name@company.ae"
                  autoComplete="email"
                  disabled={isSubmitting}
                  required
                  className="block w-full rounded-[10px] border border-[#E2E4E9] bg-white pl-10 pr-3.5 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none disabled:bg-gray-50 disabled:text-[#9CA3AF] disabled:cursor-not-allowed transition duration-150"
                />
              </div>
            </div>

            <div>
              <label
                htmlFor="password"
                className="block text-sm font-medium text-[#16181D] mb-1.5"
              >
                Password
              </label>
              <div className="relative">
                <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#9CA3AF]">
                  <Lock className="h-4 w-4" />
                </div>
                <input
                  id="password"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={(event) => setPassword(event.target.value)}
                  placeholder="••••••••"
                  autoComplete="current-password"
                  disabled={isSubmitting}
                  required
                  className="block w-full rounded-[10px] border border-[#E2E4E9] bg-white pl-10 pr-10 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none disabled:bg-gray-50 disabled:text-[#9CA3AF] disabled:cursor-not-allowed transition duration-150"
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  disabled={isSubmitting}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  className="absolute inset-y-0 right-0 flex items-center pr-3.5 text-[#9CA3AF] hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] transition-colors cursor-pointer disabled:cursor-not-allowed"
                >
                  {showPassword ? (
                    <EyeOff className="h-4 w-4" />
                  ) : (
                    <Eye className="h-4 w-4" />
                  )}
                </button>
              </div>
            </div>

            <div className="pt-1">
              <button
                type="submit"
                disabled={isSubmitting}
                className="w-full flex items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
              >
                {isSubmitting ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin text-[#0F9D74]" />
                    <span>Signing in...</span>
                  </>
                ) : (
                  <span>Sign in</span>
                )}
              </button>
            </div>
          </form>

          {/* Security & Compliance Footer */}
          <div className="mt-6 pt-4 border-t border-[#E2E4E9] flex items-center justify-center gap-2 text-xs text-[#6B7280]">
            <ShieldCheck className="h-4 w-4 text-[#0F9D74] shrink-0" />
            <span>Authorized access only &bull; End-to-end encrypted</span>
          </div>
        </div>

        {/* Page Footer */}
        <div className="mt-8 text-center text-xs text-[#9CA3AF]">
          &copy; {new Date().getFullYear()} FASCO PRO Service. All rights reserved.
        </div>
      </div>
    </div>
  )
}

export default Login