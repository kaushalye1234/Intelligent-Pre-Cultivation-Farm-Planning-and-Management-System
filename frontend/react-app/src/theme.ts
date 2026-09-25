import { useSyncExternalStore } from 'react'

export type ThemeMode = 'light' | 'dark'

export const THEME_STORAGE_KEY = 'agriassist-theme'

function readSavedTheme(): ThemeMode | null {
  try {
    const saved = window.localStorage.getItem(THEME_STORAGE_KEY)
    if (saved === 'light' || saved === 'dark') return saved
  } catch {
    // Storage can be blocked (private mode, strict settings); fall back to the system preference.
  }
  return null
}

function systemDarkQuery() {
  return typeof window.matchMedia === 'function' ? window.matchMedia('(prefers-color-scheme: dark)') : null
}

function readInitialTheme(): ThemeMode {
  return readSavedTheme() ?? (systemDarkQuery()?.matches ? 'dark' : 'light')
}

function applyTheme(theme: ThemeMode) {
  const root = document.documentElement
  root.dataset.theme = theme
  root.style.colorScheme = theme
}

/*
 * One app-wide theme store. The active mode lives on <html data-theme>, so every page and shared
 * component reads the same CSS variables from src/styles/theme.css. No provider is needed: any
 * component that calls useThemeMode() subscribes to this store.
 */
let currentTheme: ThemeMode = readInitialTheme()
const listeners = new Set<() => void>()
applyTheme(currentTheme)

export function setTheme(theme: ThemeMode) {
  currentTheme = theme
  applyTheme(theme)
  try {
    window.localStorage.setItem(THEME_STORAGE_KEY, theme)
  } catch {
    // Not persisting is acceptable; the toggle still works for this session.
  }
  listeners.forEach((listener) => listener())
}

// Follow the operating-system setting until the user picks a mode explicitly.
systemDarkQuery()?.addEventListener?.('change', (event) => {
  if (readSavedTheme()) return
  currentTheme = event.matches ? 'dark' : 'light'
  applyTheme(currentTheme)
  listeners.forEach((listener) => listener())
})

function subscribe(listener: () => void) {
  listeners.add(listener)
  return () => listeners.delete(listener)
}

function getSnapshot() {
  return currentTheme
}

export function useThemeMode() {
  const theme = useSyncExternalStore(subscribe, getSnapshot, getSnapshot)
  return {
    theme,
    setTheme,
    toggleTheme: () => setTheme(currentTheme === 'dark' ? 'light' : 'dark'),
  }
}
