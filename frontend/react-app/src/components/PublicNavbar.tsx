import { ArrowRight, Menu, Sprout, X } from 'lucide-react'
import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getDashboardPath } from '../routing'
import { ThemeToggle } from './ThemeToggle'

const publicLinks = [
  { to: '/', label: 'Home' },
  { to: '/about', label: 'About' },
  { to: '/contact', label: 'Contact' },
]

export function PublicNavbar() {
  const [isOpen, setIsOpen] = useState(false)
  const { isAuthenticated, user } = useAuth()
  const close = () => setIsOpen(false)

  return (
    <header className="public-navbar">
      <div className="public-navbar-inner">
        <NavLink to="/" className="public-brand" onClick={close}>
          <span className="public-brand-mark"><Sprout size={20} aria-hidden="true" /></span>
          <span>AgriAssist</span>
        </NavLink>
        <div className="public-navbar-tools">
          <ThemeToggle className="public-theme-toggle" />
          <button
            type="button"
            className="icon-button public-menu-button"
            onClick={() => setIsOpen((current) => !current)}
            aria-expanded={isOpen}
            aria-controls="public-nav-links"
            aria-label="Toggle navigation"
          >
            {isOpen ? <X size={20} aria-hidden="true" /> : <Menu size={20} aria-hidden="true" />}
          </button>
        </div>
        <nav id="public-nav-links" className={`public-nav-links ${isOpen ? 'open' : ''}`} aria-label="Public navigation">
          {publicLinks.map((link) => (
            <NavLink key={link.to} to={link.to} end={link.to === '/'} onClick={close}>
              {link.label}
            </NavLink>
          ))}
          <NavLink className="ui-button ui-button-primary public-login-link" to={isAuthenticated ? getDashboardPath(user?.role) : '/login'} onClick={close}>
            <span>{isAuthenticated ? 'Dashboard' : 'Staff Login'}</span>
            <ArrowRight size={16} aria-hidden="true" />
          </NavLink>
        </nav>
      </div>
    </header>
  )
}
