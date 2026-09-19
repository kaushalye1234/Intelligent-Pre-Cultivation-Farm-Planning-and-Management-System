import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { api, setAuthToken } from '../api/client'
import { useCallback } from 'react'
import type { AuthResponse, LoginResult, UserProfile } from '../types'

type AuthContextValue = {
  user: UserProfile | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  passwordChangeUser: UserProfile | null
  hasPasswordChangeSession: boolean
  login: (email: string, password: string) => Promise<LoginResult>
  changeTemporaryPassword: (newPassword: string) => Promise<UserProfile>
  logout: () => void
}

const storageKey = 'agriassist.auth'

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

type StoredSession = {
  token: string
  user: UserProfile
}

type PasswordChangeSession = {
  token: string
  expiresAt: string
  user: UserProfile
}

// Reads the JWT "exp" claim; a token we cannot decode is treated as expired.
function isTokenExpired(token: string) {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/'))) as { exp?: number }
    return !payload.exp || payload.exp * 1000 <= Date.now()
  } catch {
    return true
  }
}

function readStoredSession(): StoredSession | null {
  try {
    const stored = window.localStorage.getItem(storageKey)
    if (!stored) return null
    const parsed = JSON.parse(stored) as StoredSession
    if (parsed?.token && !isTokenExpired(parsed.token)) return parsed
  } catch {
    // Corrupt value - fall through and clear it.
  }
  window.localStorage.removeItem(storageKey)
  return null
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(null)
  const [passwordChangeSession, setPasswordChangeSession] = useState<PasswordChangeSession | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const logout = useCallback(() => {
    window.localStorage.removeItem(storageKey)
    setAuthToken(null)
    setSession(null)
    setPasswordChangeSession(null)
  }, [])

  useEffect(() => {
    const stored = readStoredSession()
    if (stored) {
      setSession(stored)
      setAuthToken(stored.token)
    }
    setIsLoading(false)
  }, [])

  // If the API rejects our token (expired, or signed with a different secret), send the user back to login.
  useEffect(() => {
    const interceptorId = api.interceptors.response.use(undefined, (error) => {
      if (error?.response?.status === 401 && !String(error.config?.url ?? '').includes('/auth/login')) {
        logout()
      }
      return Promise.reject(error)
    })
    return () => api.interceptors.response.eject(interceptorId)
  }, [logout])

  const establishSession = useCallback((authentication: AuthResponse) => {
    if (!authentication.accessToken) throw new Error('The server did not return an access token.')
    const nextSession = {
      token: authentication.accessToken,
      user: authentication.user,
    }
    window.localStorage.setItem(storageKey, JSON.stringify(nextSession))
    setAuthToken(nextSession.token)
    setSession(nextSession)
    setPasswordChangeSession(null)
  }, [])

  const login = useCallback(async (email: string, password: string): Promise<LoginResult> => {
    const response = await api.post<AuthResponse>('/auth/login', { email, password })
    const authentication = response.data
    if (authentication.authenticationStatus === 'passwordChangeRequired') {
      if (!authentication.passwordChangeToken || !authentication.passwordChangeTokenExpiresAt) {
        throw new Error('The server did not return a password-change token.')
      }

      window.localStorage.removeItem(storageKey)
      setAuthToken(null)
      setSession(null)
      setPasswordChangeSession({
        token: authentication.passwordChangeToken,
        expiresAt: authentication.passwordChangeTokenExpiresAt,
        user: authentication.user,
      })
    } else {
      establishSession(authentication)
    }

    return { status: authentication.authenticationStatus, user: authentication.user }
  }, [establishSession])

  const changeTemporaryPassword = useCallback(async (newPassword: string): Promise<UserProfile> => {
    if (!passwordChangeSession) throw new Error('Sign in again with your temporary password to continue.')
    const response = await api.post<AuthResponse>(
      '/auth/change-temporary-password',
      { newPassword },
      { headers: { Authorization: `Bearer ${passwordChangeSession.token}` } },
    )
    if (response.data.authenticationStatus !== 'authenticated') {
      throw new Error('The password change did not create a normal session.')
    }

    establishSession(response.data)
    return response.data.user
  }, [establishSession, passwordChangeSession])

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: Boolean(session?.token),
      isLoading,
      passwordChangeUser: passwordChangeSession?.user ?? null,
      hasPasswordChangeSession: Boolean(passwordChangeSession),
      login,
      changeTemporaryPassword,
      logout,
    }),
    [changeTemporaryPassword, isLoading, login, logout, passwordChangeSession, session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider')
  }

  return context
}
