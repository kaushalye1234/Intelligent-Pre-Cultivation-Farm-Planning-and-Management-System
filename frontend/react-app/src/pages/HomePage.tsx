import { ArrowRight, CalendarCheck, ClipboardCheck, PackageCheck, ShieldCheck, Sprout, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import heroImage from '../assets/hero.png'

const features = [
  {
    title: 'Crop & Season Planning',
    description: 'Coordinate farms, fields, crop types and planning requests across seasonal workflows.',
    icon: Sprout,
  },
  {
    title: 'Field Inspection & Crop Issues',
    description: 'Track scheduled inspections, field observations and crop issues that need staff attention.',
    icon: ClipboardCheck,
  },
  {
    title: 'Resource & Inventory Management',
    description: 'Manage agricultural resources, supplier details, stock levels and operational reservations.',
    icon: PackageCheck,
  },
  {
    title: 'Farm Tasks, Scheduling & Approvals',
    description: 'Organize farm tasks, irrigation schedules and manual approval decisions in one staff portal.',
    icon: CalendarCheck,
  },
]

const roles = [
  ['Farmer', 'Submits crop planning requests and works mainly through the mobile experience.'],
  ['Field Officer', 'Schedules inspections, records field observations and reports crop issues.'],
  ['Resource Officer', 'Maintains resource catalogues, inventory levels and reservations.'],
  ['Agricultural Officer', 'Reviews plans, tasks, schedules and approval decisions.'],
  ['Admin', 'Manages staff access and monitors the full operations platform.'],
]

export function HomePage() {
  return (
    <div className="public-page">
      <section className="hero-section" style={{ backgroundImage: `linear-gradient(90deg, rgba(18, 35, 25, 0.82), rgba(18, 35, 25, 0.42)), url(${heroImage})` }}>
        <div className="hero-content">
          <p>AgriAssist</p>
          <h1>Smart Agricultural Planning and Farm Operations Management</h1>
          <span>
            AgriAssist helps farmers and agricultural staff coordinate crop planning, field inspections, resources,
            schedules and manual approvals through a connected operations platform.
          </span>
          <div className="hero-actions">
            <Link className="ui-button ui-button-primary" to="/about">
              <span>Learn More</span>
            </Link>
            <Link className="ui-button ui-button-secondary hero-secondary" to="/login">
              <span>Staff Login</span>
            </Link>
          </div>
        </div>
      </section>

      <section className="public-section">
        <div className="public-section-heading">
          <p>Core Features</p>
          <h2>Built around real agricultural workflows</h2>
        </div>
        <div className="feature-grid">
          {features.map((feature) => {
            const Icon = feature.icon
            return (
              <article className="feature-card" key={feature.title}>
                <Icon size={24} aria-hidden="true" />
                <h3>{feature.title}</h3>
                <p>{feature.description}</p>
              </article>
            )
          })}
        </div>
      </section>

      <section className="public-section process-section">
        <div className="public-section-heading">
          <p>How It Works</p>
          <h2>From request to approval</h2>
        </div>
        <ol className="process-list">
          {['Farmer submits crop plan request', 'Field inspection', 'Resource and weather review', 'Task and schedule management', 'Agricultural officer approval'].map((step) => (
            <li key={step}>
              <span>{step}</span>
              <ArrowRight size={16} aria-hidden="true" />
            </li>
          ))}
        </ol>
        <p className="public-muted">The platform is designed with an AI-ready architecture for future intelligent planning support. Real Agentic AI is not active in this phase.</p>
      </section>

      <section className="public-section">
        <div className="public-section-heading">
          <p>User Roles</p>
          <h2>Clear responsibilities across the farm operation</h2>
        </div>
        <div className="role-card-grid">
          {roles.map(([title, description]) => (
            <article className="role-card" key={title}>
              <Users size={20} aria-hidden="true" />
              <h3>{title}</h3>
              <p>{description}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="public-cta">
        <ShieldCheck size={28} aria-hidden="true" />
        <div>
          <h2>Staff member?</h2>
          <p>Sign in to access your AgriAssist operations dashboard.</p>
        </div>
        <Link className="ui-button ui-button-primary" to="/login">
          <span>Staff Login</span>
        </Link>
      </section>
    </div>
  )
}
