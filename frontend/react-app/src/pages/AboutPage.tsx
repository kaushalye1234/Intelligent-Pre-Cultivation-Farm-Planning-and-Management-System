import { Cloud, Code2, Database, Leaf, MonitorSmartphone, ServerCog, Smartphone } from 'lucide-react'

const stack = [
  { name: 'ASP.NET Core', icon: ServerCog },
  { name: 'PostgreSQL / Supabase', icon: Database },
  { name: 'React', icon: Code2 },
  { name: 'Flutter', icon: Smartphone },
  { name: 'Cloudinary', icon: Cloud },
  { name: 'AI-ready architecture', icon: MonitorSmartphone },
]

export function AboutPage() {
  return (
    <div className="public-page public-content-page">
      <section className="public-title-band">
        <p>About Us</p>
        <h1>AgriAssist connects planning, field operations, resources and approvals.</h1>
        <span>
          The project supports agricultural teams that need clearer coordination between farmer requests,
          field inspections, inventory decisions and officer approvals.
        </span>
      </section>

      <section className="public-section split-section">
        <div>
          <p className="section-eyebrow">Purpose</p>
          <h2>What AgriAssist is</h2>
          <p>
            AgriAssist is an agricultural operations platform with a React staff portal, Flutter mobile foundation,
            ASP.NET Core API and PostgreSQL/Supabase data layer. It centralizes the core records needed for crop
            planning, inspections, resources, schedules and manual approvals.
          </p>
        </div>
        <div>
          <p className="section-eyebrow">Problem</p>
          <h2>What it solves</h2>
          <p>
            Agricultural work often crosses several people and records. AgriAssist reduces disconnected handoffs by
            giving staff a shared operational view while keeping farmers focused on the mobile-first experience.
          </p>
        </div>
      </section>

      <section className="public-section">
        <div className="public-section-heading">
          <p>Objectives</p>
          <h2>Project goals</h2>
        </div>
        <div className="objective-grid">
          <article>
            <Leaf size={22} aria-hidden="true" />
            <h3>Coordinate seasonal planning</h3>
            <p>Manage farms, fields, crop types and planning requests through structured workflows.</p>
          </article>
          <article>
            <Leaf size={22} aria-hidden="true" />
            <h3>Improve field visibility</h3>
            <p>Record inspections, observations and crop issues for staff review.</p>
          </article>
          <article>
            <Leaf size={22} aria-hidden="true" />
            <h3>Support controlled approvals</h3>
            <p>Keep task and schedule decisions manual, auditable and role-aware.</p>
          </article>
        </div>
      </section>

      <section className="public-section stack-section">
        <div className="public-section-heading">
          <p>Technology</p>
          <h2>Actual current stack</h2>
        </div>
        <div className="stack-grid">
          {stack.map(({ name, icon: Icon }) => (
            <span key={name}>
              <Icon size={18} aria-hidden="true" />
              {name}
            </span>
          ))}
        </div>
        <p className="public-muted">
          The system is designed with an AI-ready architecture for future multi-agent planning and decision support.
          Real Agentic AI is not implemented in the current foundation.
        </p>
      </section>
    </div>
  )
}

