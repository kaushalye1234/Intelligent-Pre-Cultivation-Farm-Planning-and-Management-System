import { Menu, Sprout, X } from 'lucide-react'
import { useState } from 'react'
import { NavLink } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { getDashboardPath } from '../routing'

const publicLinks = [
  { to: '/', label: 'Home' },
  { to: '/about', label: 'About Us' },
  { to: '/contact', label: 'Contact Us' },
]

export function PublicNavbar() {
  const [isOpen, setIsOpen] = useState(false)
  const { isAuthenticated, user } = useAuth()

  return (
    <header className="public-navbar">
      <NavLink to="/" className="public-brand" onClick={() => setIsOpen(false)}>
        <Sprout size={28} aria-hidden="true" />
        <span>AgriAssist</span>
      </NavLink>
      <button type="button" className="public-menu-button" onClick={() => setIsOpen((current) => !current)} aria-expanded={isOpen} aria-label="Toggle navigation">
        {isOpen ? <X size={20} aria-hidden="true" /> : <Menu size={20} aria-hidden="true" />}
      </button>
      <nav className={`public-nav-links ${isOpen ? 'open' : ''}`} aria-label="Public navigation">
        {publicLinks.map((link) => (
          <NavLink key={link.to} to={link.to} onClick={() => setIsOpen(false)}>
            {link.label}
          </NavLink>
        ))}
        <NavLink className="public-login-link" to={isAuthenticated ? getDashboardPath(user?.role) : '/login'} onClick={() => setIsOpen(false)}>
          {isAuthenticated ? 'Dashboard' : 'Staff Login'}
        </NavLink>
      </nav>
    </header>
  )
}
