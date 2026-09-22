import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, getErrorMessage } from './api/client'
import { AppRoutes } from './App'
import { AuthContext } from './auth/AuthContext'
import { DataTable } from './components/DataTable'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { CropPlanningPage } from './pages/CropPlanningPage'
import { LoginPage } from './pages/LoginPage'
import type { UserProfile } from './types'

const adminUser: UserProfile = {
  id: 'admin-user-id',
  fullName: 'Development Admin',
  email: 'admin@agriassist.local',
  role: 5,
  isActive: true,
  mustChangePassword: false,
}

const fieldOfficer: UserProfile = {
  id: 'field-user-id',
  fullName: 'Field Officer',
  email: 'field@agriassist.local',
  role: 2,
  isActive: true,
  mustChangePassword: false,
}

function authValue(overrides: Partial<React.ContextType<typeof AuthContext>> = {}) {
  return {
    user: null,
    token: null,
    isAuthenticated: false,
    isLoading: false,
    passwordChangeUser: null,
    hasPasswordChangeSession: false,
    login: vi.fn(),
    changeTemporaryPassword: vi.fn(),
    logout: vi.fn(),
    ...overrides,
  }
}

function renderAppRoute(route: string, overrides: Partial<React.ContextType<typeof AuthContext>> = {}) {
  return render(
    <MemoryRouter initialEntries={[route]}>
      <AuthContext.Provider value={authValue(overrides)}>
        <AppRoutes />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

function renderLogin(login = vi.fn()) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue({ login })}>
        <LoginPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

afterEach(() => {
  vi.restoreAllMocks()
  window.localStorage.clear()
})

describe('React public website and portal routing', () => {
  it('renders the public home route at root', () => {
    renderAppRoute('/')

    expect(screen.getByRole('heading', { name: /smart agricultural planning/i })).toBeInTheDocument()
    expect(within(screen.getByRole('navigation', { name: /public navigation/i })).getByRole('link', { name: /staff login/i })).toBeInTheDocument()
  })

  it('renders public about and contact routes', () => {
    renderAppRoute('/about')
    expect(screen.getByRole('heading', { name: /agriassist connects planning/i })).toBeInTheDocument()

    renderAppRoute('/contact')
    expect(screen.getByRole('heading', { name: /project contact/i })).toBeInTheDocument()
  })

  it('navigates with public navbar links', async () => {
    renderAppRoute('/')

    await userEvent.click(within(screen.getByRole('navigation', { name: /public navigation/i })).getByRole('link', { name: /about us/i }))
    expect(screen.getByRole('heading', { name: /agriassist connects planning/i })).toBeInTheDocument()
  })

  it('validates login form before calling the API', async () => {
    const login = vi.fn()
    renderLogin(login)

    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email and password are required.')
    expect(login).not.toHaveBeenCalled()
  })

  it('shows login error state from failed API login', async () => {
    const login = vi.fn().mockRejectedValue(new Error('Invalid credentials'))
    renderLogin(login)

    await userEvent.type(screen.getByLabelText(/email/i), 'admin@agriassist.local')
    await userEvent.type(screen.getByLabelText(/password/i), 'wrong-password')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid credentials')
  })

  it('redirects successful admin login to the admin dashboard route', async () => {
    const login = vi.fn().mockResolvedValue({ status: 'authenticated', user: adminUser })
    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthContext.Provider value={authValue({ login })}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/admin/dashboard" element={<div>Admin dashboard target</div>} />
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await userEvent.type(screen.getByLabelText(/email/i), 'admin@agriassist.local')
    await userEvent.type(screen.getByLabelText(/password/i), 'Admin@2026')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('Admin dashboard target')).toBeInTheDocument()
  })

  it('redirects protected routes to login when unauthenticated', () => {
    render(
      <MemoryRouter initialEntries={['/dashboard']}>
        <AuthContext.Provider value={authValue()}>
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route path="/dashboard" element={<div>Private dashboard</div>} />
            </Route>
            <Route path="/login" element={<div>Login target</div>} />
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByText('Login target')).toBeInTheDocument()
  })

  it('shows role-aware navigation in the authenticated shell', () => {
    render(
      <MemoryRouter initialEntries={['/inspections/dashboard']}>
        <AuthContext.Provider value={authValue({ user: fieldOfficer, token: 'token', isAuthenticated: true })}>
          <Routes>
            <Route element={<Layout />}>
              <Route path="/inspections/dashboard" element={<div>Inspection dashboard content</div>} />
            </Route>
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByRole('link', { name: /dashboard/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /inspections/i })).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /users/i })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: /^resources$/i })).not.toBeInTheDocument()
  })

  it('opens a focused create modal from a module page', async () => {
    const emptyPage = { data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } }
    vi.spyOn(api, 'get').mockResolvedValue(emptyPage)

    render(
      <MemoryRouter>
        <CropPlanningPage />
      </MemoryRouter>,
    )

    await waitFor(() => expect(api.get).toHaveBeenCalled())
    await userEvent.click(screen.getByRole('button', { name: /add farm/i }))

    expect(screen.getByRole('dialog', { name: /add farm/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/total area/i)).toBeInTheDocument()
  })

  it('shows empty table state', () => {
    render(<DataTable rows={[]} emptyMessage="No rows here." columns={[{ header: 'Name', render: () => 'x' }]} />)

    expect(screen.getByText('No rows here.')).toBeInTheDocument()
  })

  it('normalizes unknown API errors', () => {
    expect(getErrorMessage('bad')).toBe('Unexpected error')
  })
})

