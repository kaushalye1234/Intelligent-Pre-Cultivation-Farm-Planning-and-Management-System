import { ArrowRight, CalendarCheck, ClipboardCheck, PackageCheck, ShieldCheck, Sprout, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import './HomePage.css'
import { HomeGrowthPlant } from './HomeGrowthPlant'
import { useHomeScrollReveal } from './useHomeScrollReveal'

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

const processSteps = [
  ['Request', 'Farmer submits crop plan request'],
  ['Observe', 'Field inspection'],
  ['Prepare', 'Resource and weather review'],
  ['Coordinate', 'Task and schedule management'],
  ['Decide', 'Agricultural officer approval'],
]

export function HomePage() {
  const pageRef = useHomeScrollReveal()

  return (
    <div ref={pageRef} className="public-page home-page">
      <section className="hero-section home-hero" aria-labelledby="home-hero-title">
        <div className="home-hero-glow" aria-hidden="true" />
        <div className="home-hero-grid">
          <div className="hero-content home-hero-content" data-home-reveal="hero">
            <p className="home-eyebrow"><span aria-hidden="true" />AgriAssist</p>
            <h1 id="home-hero-title">Smart agricultural planning, <em>rooted in real farm operations.</em></h1>
            <p className="home-hero-summary">
              AgriAssist helps farmers and agricultural staff coordinate crop planning, field inspections, resources,
              schedules and manual approvals through a connected operations platform.
            </p>
            <div className="hero-actions home-hero-actions">
              <Link className="ui-button ui-button-primary home-primary-action" to="/about">
                <span>Learn More</span>
                <ArrowRight size={17} aria-hidden="true" />
              </Link>
              <Link className="ui-button ui-button-secondary hero-secondary" to="/login">
                <span>Staff Login</span>
              </Link>
            </div>
            <div className="home-hero-assurance" aria-label="Platform strengths">
              <span>Plan</span>
              <span>Coordinate</span>
              <span>Approve</span>
            </div>
          </div>

          <div className="home-hero-landscape" aria-hidden="true" data-home-reveal="hero-visual">
            <div className="home-field-sun" />
            <div className="home-field-horizon" />
            <div className="home-field-plot">
              <span className="home-field-row home-field-row-one" />
              <span className="home-field-row home-field-row-two" />
              <span className="home-field-row home-field-row-three" />
              <span className="home-field-row home-field-row-four" />
            </div>
            <div className="home-field-marker home-field-marker-one"><Sprout size={18} /></div>
            <div className="home-field-marker home-field-marker-two"><ClipboardCheck size={18} /></div>
            <div className="home-field-marker home-field-marker-three"><ShieldCheck size={18} /></div>
            <div className="home-landscape-label">
              <span>One connected journey</span>
              <strong>From field insight to confident action</strong>
            </div>
          </div>
        </div>
      </section>

      <div className="home-growth-journey">
        <HomeGrowthPlant />
        <section id="home-features" className="public-section home-section home-features" aria-labelledby="home-features-title">
          <div className="home-heading-row">
            <div className="public-section-heading" data-home-reveal="heading">
              <p>Core Features</p>
              <h2 id="home-features-title">Built around real agricultural workflows</h2>
            </div>
            <p className="home-section-intro" data-home-reveal="note">
              One connected workspace supports every stage of cultivation while keeping people responsible for each decision.
            </p>
          </div>
          <div className="feature-grid home-feature-grid">
            {features.map((feature, index) => {
              const Icon = feature.icon
              return (
                <article className="feature-card home-feature-card" key={feature.title} data-home-reveal="card">
                  <div className="home-card-topline">
                    <span>0{index + 1}</span>
                    <div className="home-feature-icon"><Icon size={24} aria-hidden="true" /></div>
                  </div>
                  <h3>{feature.title}</h3>
                  <p>{feature.description}</p>
                  <span className="home-card-detail" aria-hidden="true">Connected operation <ArrowRight size={15} /></span>
                </article>
              )
            })}
          </div>
        </section>

        <section className="public-section process-section home-section home-process" aria-labelledby="home-process-title">
          <div className="public-section-heading" data-home-reveal="heading">
            <p>How It Works</p>
            <h2 id="home-process-title">From request to approval</h2>
          </div>
          <ol className="process-list home-process-list" data-home-reveal="group">
            {processSteps.map(([phaseName, step], index) => (
              <li key={step}>
                <span className="home-process-number">0{index + 1}</span>
                <div>
                  <small>{phaseName}</small>
                  <span>{step}</span>
                </div>
                <ArrowRight size={17} aria-hidden="true" />
              </li>
            ))}
          </ol>
          <div className="home-project-note" data-home-reveal="note">
            <Sprout size={21} aria-hidden="true" />
            <div>
              <strong>AI-ready foundation</strong>
              <p className="public-muted">The platform is designed with an AI-ready architecture for future intelligent planning support. Real Agentic AI is not active in this phase.</p>
            </div>
          </div>
        </section>

        <section className="public-section home-section home-roles" aria-labelledby="home-roles-title">
          <div className="home-heading-row">
            <div className="public-section-heading" data-home-reveal="heading">
              <p>User Roles</p>
              <h2 id="home-roles-title">Clear responsibilities across the farm operation</h2>
            </div>
            <p className="home-section-intro" data-home-reveal="note">
              Role-aware access keeps each team focused while the wider agricultural operation stays connected.
            </p>
          </div>
          <div className="role-card-grid home-role-grid">
            {roles.map(([title, description], index) => (
              <article className="role-card home-role-card" key={title} data-home-reveal="card">
                <div className="home-role-card-header">
                  <div className="home-role-icon"><Users size={20} aria-hidden="true" /></div>
                  <span>Role 0{index + 1}</span>
                </div>
                <h3>{title}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
        </section>
      </div>

      <section className="public-cta home-cta" aria-labelledby="home-cta-title" data-home-reveal="group">
        <div className="home-cta-icon"><ShieldCheck size={28} aria-hidden="true" /></div>
        <div>
          <span>Secure staff access</span>
          <h2 id="home-cta-title">Staff member?</h2>
          <p>Sign in to access your AgriAssist operations dashboard.</p>
        </div>
        <Link className="ui-button ui-button-primary home-cta-button" to="/login">
          <span>Staff Login</span>
          <ArrowRight size={17} aria-hidden="true" />
        </Link>
      </section>
    </div>
  )
}
