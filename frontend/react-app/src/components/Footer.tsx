import { Sprout } from 'lucide-react'
import { NavLink } from 'react-router-dom'

export function Footer() {
  return (
    <footer className="public-footer">
      <div className="public-footer-inner">
        <div className="public-footer-brand">
          <NavLink to="/" className="public-brand public-brand-inverse">
            <span className="public-brand-mark"><Sprout size={20} aria-hidden="true" /></span>
            <span>AgriAssist</span>
          </NavLink>
          <p>
            Smart agricultural planning and farm operations management — crop planning, field inspections,
            resources, weather and approvals in one platform.
          </p>
        </div>
        <nav aria-label="Footer navigation">
          <strong>Platform</strong>
          <NavLink to="/" end>Home</NavLink>
          <NavLink to="/about">About</NavLink>
          <NavLink to="/contact">Contact</NavLink>
          <NavLink to="/login">Staff Login</NavLink>
        </nav>
        <div>
          <strong>Built with</strong>
          <p>React, ASP.NET Core, PostgreSQL/Supabase, Flutter, and a FastAPI + LangGraph AI service.</p>
        </div>
      </div>
      <p className="public-footer-note">© {new Date().getFullYear()} AgriAssist · University software engineering project.</p>
    </footer>
  )
}
