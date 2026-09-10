import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getDashboardPath } from '../routing'
import type { ApplicationRole } from '../types'
import { LoadingState } from './States'

export function ProtectedRoute({ allowedRoles, children }: { allowedRoles?: ApplicationRole[]; children?: React.ReactNode }) {
  const { isAuthenticated, isLoading, user } = useAuth()

  if (isLoading) {
    return <LoadingState label="Checking session" />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  if (allowedRoles && user && !allowedRoles.includes(user.role)) {
    return <Navigate to={getDashboardPath(user.role)} replace />
  }

  if (children) {
    return <>{children}</>
  }

  return <Outlet />
}
