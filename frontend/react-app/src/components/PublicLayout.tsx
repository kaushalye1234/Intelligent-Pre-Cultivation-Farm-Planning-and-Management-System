import { Outlet } from 'react-router-dom'
import { Footer } from './Footer'
import { PublicNavbar } from './PublicNavbar'

export function PublicLayout() {
  return (
    <div className="public-shell">
      <PublicNavbar />
      <main>
        <Outlet />
      </main>
      <Footer />
    </div>
  )
}
