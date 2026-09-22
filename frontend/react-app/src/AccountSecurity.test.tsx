import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api, setAuthToken } from './api/client'
import { AuthContext, AuthProvider, useAuth } from './auth/AuthContext'
import { LoginPage } from './pages/LoginPage'
import { UsersPage } from './pages/UsersPage'
import type { AuthResponse, UserProfile } from './types'

const admin: UserProfile = {
  id: 'admin-id',
  fullName: 'Current Admin',
  email: 'admin@example.test',
  role: 5,
  isActive: true,
  mustChangePassword: false,
}

const fieldOfficer: UserProfile = {
  id: 'field-officer-id',
  fullName: 'Field Officer',
  email: 'field@example.test',
  role: 2,
  isActive: true,
  mustChangePassword: true,
}

function contextValue(overrides: Partial<React.ContextType<typeof AuthContext>> = {}) {
  return {
    user: admin,
    token: 'access-token',
    isAuthenticated: true,
    isLoading: false,
    passwordChangeUser: null,
    hasPasswordChangeSession: false,
    login: vi.fn(),
    changeTemporaryPassword: vi.fn(),
    logout: vi.fn(),
    ...overrides,
  }
}

function AuthHarness() {
  const auth = useAuth()
  return (
    <div>
      <button type="button" onClick={() => void auth.login('field@example.test', 'temporary passphrase')}>Temporary login</button>
      <button type="button" onClick={() => void auth.changeTemporaryPassword('private harvest passphrase')}>Change password</button>
      <span>{auth.hasPasswordChangeSession ? 'Password change pending' : 'No password change pending'}</span>
      <span>{auth.isAuthenticated ? 'Authenticated' : 'Not authenticated'}</span>
    </div>
  )
}

afterEach(() => {
  vi.restoreAllMocks()
  window.localStorage.clear()
  setAuthToken(null)
})

describe('React account security workflows', () => {
  it('keeps the password-change token out of persistent auth storage and stores only the replacement access session', async () => {
    const temporaryResponse: AuthResponse = {
      authenticationStatus: 'passwordChangeRequired',
      accessToken: null,
      accessTokenExpiresAt: null,
      passwordChangeToken: 'temporary-change-token',
      passwordChangeTokenExpiresAt: '2026-09-19T01:00:00Z',
      user: fieldOfficer,
    }
    const authenticatedResponse: AuthResponse = {
      authenticationStatus: 'authenticated',
      accessToken: 'new-access-token',
      accessTokenExpiresAt: '2026-09-19T02:00:00Z',
      passwordChangeToken: null,
      passwordChangeTokenExpiresAt: null,
      user: { ...fieldOfficer, mustChangePassword: false },
    }
    const post = vi.spyOn(api, 'post')
      .mockResolvedValueOnce({ data: temporaryResponse })
      .mockResolvedValueOnce({ data: authenticatedResponse })
    render(<AuthProvider><AuthHarness /></AuthProvider>)

    await userEvent.click(screen.getByRole('button', { name: /temporary login/i }))

    expect(await screen.findByText('Password change pending')).toBeInTheDocument()
    expect(window.localStorage.getItem('agriassist.auth')).toBeNull()

    await userEvent.click(screen.getByRole('button', { name: /change password/i }))

    expect(await screen.findByText('Authenticated')).toBeInTheDocument()
    expect(post).toHaveBeenNthCalledWith(
      2,
      '/auth/change-temporary-password',
      { newPassword: 'private harvest passphrase' },
      { headers: { Authorization: 'Bearer temporary-change-token' } },
    )
    const storedSession = window.localStorage.getItem('agriassist.auth') ?? ''
    expect(storedSession).toContain('new-access-token')
    expect(storedSession).not.toContain('temporary-change-token')
  })

  it('routes a temporary-password login to the first-login password screen', async () => {
    const login = vi.fn().mockResolvedValue({ status: 'passwordChangeRequired', user: fieldOfficer })
    render(
      <MemoryRouter initialEntries={['/login']}>
        <AuthContext.Provider value={contextValue({ user: null, token: null, isAuthenticated: false, login })}>
          <Routes>
            <Route path="/login" element={<LoginPage />} />
            <Route path="/change-temporary-password" element={<div>First-login password target</div>} />
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await userEvent.type(screen.getByLabelText(/email address/i), fieldOfficer.email)
    await userEvent.type(screen.getByLabelText(/^password$/i), 'temporary passphrase')
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByText('First-login password target')).toBeInTheDocument()
  })

  it('creates staff through the exact admin endpoint without exposing a Farmer role option', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [], page: 1, pageSize: 20, totalCount: 0, totalPages: 0 } })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    render(
      <MemoryRouter>
        <AuthContext.Provider value={contextValue()}><UsersPage /></AuthContext.Provider>
      </MemoryRouter>,
    )
    await waitFor(() => expect(api.get).toHaveBeenCalled())

    await userEvent.click(screen.getByRole('button', { name: /create staff account/i }))
    const createDialog = screen.getByRole('dialog', { name: /create staff account/i })
    expect(within(createDialog).queryByRole('option', { name: 'Farmer' })).not.toBeInTheDocument()
    fireEvent.change(within(createDialog).getByLabelText(/full name/i), { target: { value: 'New Field Officer' } })
    fireEvent.change(within(createDialog).getByLabelText(/^email/i), { target: { value: 'new.field@example.test' } })
    fireEvent.change(within(createDialog).getByLabelText(/staff role/i), { target: { value: '2' } })
    fireEvent.change(within(createDialog).getByLabelText(/^temporary password/i), { target: { value: 'temporary harvest phrase' } })
    fireEvent.change(within(createDialog).getByLabelText(/confirm temporary password/i), { target: { value: 'temporary harvest phrase' } })
    await userEvent.click(screen.getByRole('button', { name: /^create account$/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/admin/users', {
      fullName: 'New Field Officer',
      email: 'new.field@example.test',
      role: 2,
      temporaryPassword: 'temporary harvest phrase',
      currentAdminPassword: null,
    }))
    expect(post.mock.calls.some(([url]) => url === '/users')).toBe(false)
  })

  it('resets staff through the exact reset endpoint', async () => {
    vi.spyOn(api, 'get').mockResolvedValue({ data: { items: [fieldOfficer], page: 1, pageSize: 20, totalCount: 1, totalPages: 1 } })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} })
    render(
      <MemoryRouter>
        <AuthContext.Provider value={contextValue()}><UsersPage /></AuthContext.Provider>
      </MemoryRouter>,
    )
    await screen.findByText(fieldOfficer.fullName)

    await userEvent.click(screen.getByLabelText(`Actions for ${fieldOfficer.fullName}`))
    await userEvent.click(screen.getByRole('button', { name: /reset password/i }))
    const resetDialog = screen.getByRole('dialog', { name: /reset staff password/i })
    await userEvent.type(screen.getByLabelText(/^temporary password/i), 'replacement harvest phrase')
    await userEvent.type(screen.getByLabelText(/confirm temporary password/i), 'replacement harvest phrase')
    await userEvent.click(within(resetDialog).getByRole('button', { name: /^reset password$/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith(
      '/admin/users/field-officer-id/reset-password',
      { temporaryPassword: 'replacement harvest phrase', currentAdminPassword: null },
    ))
  })
})
