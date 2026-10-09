import { Moon, Sun } from 'lucide-react'
import { useThemeMode } from '../theme'

/** The single light/dark switch. Every instance drives the same app-wide theme store. */
export function ThemeToggle({ className = '' }: { className?: string }) {
  const { theme, toggleTheme } = useThemeMode()
  const isDark = theme === 'dark'
  return (
    <button
      type="button"
      role="switch"
      aria-checked={isDark}
      aria-label="Dark mode"
      title={isDark ? 'Switch to light mode' : 'Switch to dark mode'}
      className={`theme-toggle ${className}`.trim()}
      onClick={toggleTheme}
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
