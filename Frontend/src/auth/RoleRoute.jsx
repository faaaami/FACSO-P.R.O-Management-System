import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

function RoleRoute({ allowedRoles }) {
  const { user, isAuthenticated, isInitializing } = useAuth()

  if (isInitializing) {
    return <div>Loading...</div>
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (!allowedRoles.includes(user.role)) {
    return <Navigate to="/" replace />
  }

  return <Outlet />
}

export default RoleRoute