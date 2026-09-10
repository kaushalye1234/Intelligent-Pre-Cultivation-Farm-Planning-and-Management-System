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

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const stored = window.localStorage.getItem(storageKey)
    if (stored) {
      const parsed = JSON.parse(stored) as StoredSession
      setSession(parsed)
      setAuthToken(parsed.token)
    }
    setIsLoading(false)
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
