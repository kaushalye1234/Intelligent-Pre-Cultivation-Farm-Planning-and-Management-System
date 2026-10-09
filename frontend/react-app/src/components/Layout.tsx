import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom'
import { Home, Info, LogOut, Menu, X } from 'lucide-react'
import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { roleLabels } from '../labels'
import { getNavigationGroups, getSectionContext, portalIcon } from '../routing'
import { ThemeToggle } from './ThemeToggle'
import { Button } from './Ui'

export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [isDrawerOpen, setIsDrawerOpen] = useState(false)
  const navGroups = getNavigationGroups(user?.role)
  const context = getSectionContext(location.pathname)
  const PortalIcon = portalIcon

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <div className="app-shell">
      <aside className={`sidebar ${isDrawerOpen ? 'open' : ''}`}>
        <div className="brand">
          <PortalIcon size={26} aria-hidden="true" />
          <div>
            <strong>AgriAssist</strong>
            <span>Operations Console</span>
          </div>
        </div>
        <nav aria-label="Main navigation">
          {navGroups.map((group) => (
            <section className="nav-group" key={group.label}>
              <p>{group.label}</p>
              {group.items.map((item) => {
                const Icon = item.icon
                return (
                  <NavLink key={item.to} to={item.to} onClick={() => setIsDrawerOpen(false)}>
                    <Icon size={18} aria-hidden="true" />
                    <span>{item.label}</span>
                  </NavLink>
                )
              })}
            </section>
          ))}
          <section className="nav-group">
            <p>Website</p>
            <NavLink to="/" end onClick={() => setIsDrawerOpen(false)}>
              <Home size={18} aria-hidden="true" />
              <span>Home</span>
            </NavLink>
            <NavLink to="/about" onClick={() => setIsDrawerOpen(false)}>
              <Info size={18} aria-hidden="true" />
              <span>About</span>
            </NavLink>
          </section>
        </nav>
        <div className="sidebar-footer">
          <div className="avatar" aria-hidden="true">{user?.fullName?.slice(0, 2).toUpperCase() ?? 'AA'}</div>
          <div>
            <strong>{user?.fullName}</strong>
            <span>{user ? roleLabels[user.role] : ''}</span>
          </div>
          <button type="button" className="icon-button" onClick={handleLogout} aria-label="Log out" title="Log out">
            <LogOut size={18} aria-hidden="true" />
          </button>
        </div>
      </aside>
      {isDrawerOpen ? <button type="button" className="drawer-scrim" aria-label="Close navigation" onClick={() => setIsDrawerOpen(false)} /> : null}
      <main className="content">
        <header className="topbar">
          <button type="button" className="icon-button mobile-menu-toggle" onClick={() => setIsDrawerOpen((current) => !current)} aria-label="Open navigation">
            {isDrawerOpen ? <X size={18} aria-hidden="true" /> : <Menu size={18} aria-hidden="true" />}
          </button>
          <div className="topbar-context">
            <span>{context.section}</span>
            <strong>{context.title}</strong>
          </div>
          <div className="topbar-user">
            <span>{user?.fullName}</span>
            <strong>{user ? roleLabels[user.role] : ''}</strong>
          </div>
          <div className="topbar-actions">
            <ThemeToggle />
            <Button variant="ghost" icon={<LogOut size={16} aria-hidden="true" />} onClick={handleLogout}>
              Logout
            </Button>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  )
}
