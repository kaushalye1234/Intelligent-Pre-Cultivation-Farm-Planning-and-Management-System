import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { PublicLayout } from './components/PublicLayout'
import { AboutPage } from './pages/AboutPage'
import { ContactPage } from './pages/ContactPage'
import { CropPlanningPage } from './pages/CropPlanningPage'
import { DashboardPage } from './pages/DashboardPage'
import { HomePage } from './pages/HomePage'
import { InspectionsPage } from './pages/InspectionsPage'
import { LoginPage } from './pages/LoginPage'
import { ResourcesPage } from './pages/ResourcesPage'
import { TaskApprovalPage } from './pages/TaskApprovalPage'
import { UsersPage } from './pages/UsersPage'
import { Roles, staffRoles } from './routing'
import './styles.css'

export function AppRoutes() {
  return (
    <Routes>
      <Route element={<PublicLayout />}>
        <Route index element={<HomePage />} />
        <Route path="about" element={<AboutPage />} />
        <Route path="contact" element={<ContactPage />} />
        <Route path="login" element={<LoginPage />} />
      </Route>
      <Route element={<ProtectedRoute allowedRoles={staffRoles} />}>
        <Route element={<Layout />}>
          <Route path="dashboard" element={<DashboardPage />} />
          <Route path="admin" element={<Navigate to="/admin/dashboard" replace />} />
          <Route path="admin/dashboard" element={<ProtectedRoute allowedRoles={[Roles.Admin]}><DashboardPage /></ProtectedRoute>} />
          <Route path="inspections/dashboard" element={<ProtectedRoute allowedRoles={[Roles.FieldOfficer]}><DashboardPage /></ProtectedRoute>} />
          <Route path="resources/dashboard" element={<ProtectedRoute allowedRoles={[Roles.ResourceOfficer]}><DashboardPage /></ProtectedRoute>} />
          <Route path="officer/dashboard" element={<ProtectedRoute allowedRoles={[Roles.AgriculturalOfficer]}><DashboardPage /></ProtectedRoute>} />
          <Route path="crop-planning" element={<ProtectedRoute allowedRoles={[Roles.Admin, Roles.AgriculturalOfficer]}><CropPlanningPage /></ProtectedRoute>} />
          <Route path="inspections" element={<ProtectedRoute allowedRoles={[Roles.Admin, Roles.FieldOfficer, Roles.AgriculturalOfficer]}><InspectionsPage /></ProtectedRoute>} />
          <Route path="resources" element={<ProtectedRoute allowedRoles={[Roles.Admin, Roles.ResourceOfficer]}><ResourcesPage /></ProtectedRoute>} />
          <Route path="task-approval" element={<ProtectedRoute allowedRoles={[Roles.Admin, Roles.FieldOfficer, Roles.AgriculturalOfficer]}><TaskApprovalPage /></ProtectedRoute>} />
          <Route path="users" element={<ProtectedRoute allowedRoles={[Roles.Admin]}><UsersPage /></ProtectedRoute>} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}
