import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { api, setAuthToken } from '../api/client'
import type { AuthResponse, UserProfile } from '../types'

type AuthContextValue = {
  user: UserProfile | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (email: string, password: string) => Promise<UserProfile>
  logout: () => void
}

const storageKey = 'agriassist.auth'

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

type StoredSession = {
  token: string
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
  const [isLoading, setIsLoading] = useState(true)

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
  }, [])

  async function login(email: string, password: string) {
    const response = await api.post<AuthResponse>('/auth/login', { email, password })
    const nextSession = {
      token: response.data.accessToken,
      user: response.data.user,
    }
    window.localStorage.setItem(storageKey, JSON.stringify(nextSession))
    setAuthToken(nextSession.token)
    setSession(nextSession)
    return nextSession.user
  }

  function logout() {
    window.localStorage.removeItem(storageKey)
    setAuthToken(null)
    setSession(null)
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: Boolean(session?.token),
      isLoading,
      login,
      logout,
    }),
    [isLoading, session],
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
