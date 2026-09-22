import { useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
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
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [error, setError] = useState(null)

  if (isInitializing) {
    return <div>Loading...</div>
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
    <form onSubmit={handleSubmit}>
      <div>
        <label htmlFor="email">Email</label>
        <input
          id="email"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          autoComplete="email"
          disabled={isSubmitting}
          required
        />
      </div>
      <div>
        <label htmlFor="password">Password</label>
        <input
          id="password"
          type="password"
          value={password}
          onChange={(event) => setPassword(event.target.value)}
          autoComplete="current-password"
          disabled={isSubmitting}
          required
        />
      </div>
      {error && <p role="alert">{error}</p>}
      <button type="submit" disabled={isSubmitting}>
        {isSubmitting ? 'Signing in...' : 'Sign in'}
      </button>
    </form>
  )
}

export default Login