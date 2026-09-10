import { NavLink } from 'react-router-dom'

export function Footer() {
  return (
    <footer className="public-footer">
      <div>
        <h2>AgriAssist</h2>
        <p>Smart Agricultural Planning & Farm Operations Management</p>
      </div>
      <nav aria-label="Footer navigation">
        <strong>Quick Links</strong>
        <NavLink to="/">Home</NavLink>
        <NavLink to="/about">About Us</NavLink>
        <NavLink to="/contact">Contact Us</NavLink>
        <NavLink to="/login">Staff Login</NavLink>
      </nav>
      <div>
        <strong>Project Platform</strong>
        <p>ASP.NET Core, PostgreSQL/Supabase, React, Flutter, Cloudinary, and an AI-ready architecture.</p>
      </div>
      <p className="footer-note">Â2026 AgriAssist. University project operations platform.</p>
    </footer>
  )
}

