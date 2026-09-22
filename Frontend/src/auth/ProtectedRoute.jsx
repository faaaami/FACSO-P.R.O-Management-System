import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from './AuthContext'

function ProtectedRoute() {
  const { isAuthenticated, isInitializing } = useAuth()

  if (isInitializing) {
    return <div>Loading...</div>
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}

export default ProtectedRoute