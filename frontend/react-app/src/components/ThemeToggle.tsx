import { Moon, Sun } from 'lucide-react'
import type { ThemeMode } from '../theme'

export function ThemeToggle({ theme, onToggle }: { theme: ThemeMode; onToggle: () => void }) {
  const isDark = theme === 'dark'
  return (
    <button
      type="button"
      role="switch"
      aria-checked={isDark}
      aria-label="Dark mode"
      title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      className="theme-toggle"
      onClick={onToggle}
    >
      <span className="theme-toggle-track" aria-hidden="true">
        <Sun size={14} className="theme-toggle-sun" />
        <Moon size={14} className="theme-toggle-moon" />
        <span className="theme-toggle-thumb" />
      </span>
      <span className="theme-toggle-label" aria-hidden="true">{isDark ? 'Dark' : 'Light'}</span>
    </button>
  )
}
