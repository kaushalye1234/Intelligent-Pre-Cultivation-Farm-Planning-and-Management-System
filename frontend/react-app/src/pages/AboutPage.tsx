import { ArrowRight, ClipboardCheck, Compass, Leaf, Route, ShieldCheck, Sprout, Workflow } from 'lucide-react'
import { Link } from 'react-router-dom'

const insightCards = [
  {
    label: 'Purpose',
    title: 'What AgriAssist is',
    description:
      'AgriAssist is an agricultural operations platform with a React staff portal, Flutter mobile foundation, ASP.NET Core API and PostgreSQL/Supabase data layer. It centralizes the core records needed for crop planning, inspections, resources, schedules and manual approvals.',
    icon: Compass,
  },
  {
    label: 'Problem',
    title: 'What it solves',
    description:
      'Agricultural work often crosses several people and records. AgriAssist reduces disconnected handoffs by giving staff a shared operational view while keeping farmers focused on the mobile-first experience.',
    icon: Workflow,
  },
]

const goals = [
  {
    number: '01',
    title: 'Coordinate seasonal planning',
    description: 'Manage farms, fields, crop types and planning requests through structured workflows.',
    icon: Sprout,
  },
  {
    number: '02',
    title: 'Improve field visibility',
    description: 'Record inspections, observations and crop issues for staff review.',
    icon: ClipboardCheck,
  },
  {
    number: '03',
    title: 'Support controlled approvals',
    description: 'Keep task and schedule decisions manual, auditable and role-aware.',
    icon: ShieldCheck,
  },
]

const flowSteps = ['Farmer Request', 'Field Review', 'Resource Coordination', 'Officer Approval']

export function AboutPage() {
  return (
    <div className="public-page public-content-page about-page">
      <section className="about-hero" aria-labelledby="about-title">
        <div className="about-hero-copy">
          <p className="about-kicker">About Us</p>
          <h1 id="about-title">AgriAssist connects planning, field operations, resources and approvals.</h1>
          <span>
            The project supports agricultural teams that need clearer coordination between farmer requests,
            field inspections, inventory decisions and officer approvals.
          </span>
        </div>
        <div className="about-hero-visual" aria-hidden="true">
          <span className="about-field-line about-field-line-one" />
          <span className="about-field-line about-field-line-two" />
          <span className="about-field-line about-field-line-three" />
          <span className="about-leaf-mark about-leaf-mark-one" />
          <span className="about-leaf-mark about-leaf-mark-two" />
        </div>
      </section>

      <section className="public-section about-insight-grid" aria-label="AgriAssist purpose and problem">
        {insightCards.map(({ label, title, description, icon: Icon }) => (
          <article className="about-insight-card" key={label}>
            <div className="about-card-icon">
              <Icon size={22} aria-hidden="true" />
            </div>
            <p className="about-kicker">{label}</p>
            <h2>{title}</h2>
            <p>{description}</p>
          </article>
        ))}
      </section>

      <section className="public-section about-goals-section">
        <div className="public-section-heading">
          <p>Objectives</p>
          <h2>Project goals</h2>
        </div>
        <div className="about-goal-grid">
          {goals.map(({ number, title, description, icon: Icon }) => (
            <article className="about-goal-card" key={title}>
              <div className="about-goal-card-top">
                <span>{number}</span>
                <Icon size={21} aria-hidden="true" />
              </div>
              <h3>{title}</h3>
              <p>{description}</p>
            </article>
          ))}
        </div>
      </section>

      <section className="public-section about-flow-section">
        <div className="public-section-heading">
          <p>Workflow</p>
          <h2>From request to action</h2>
        </div>
        <ol className="about-flow-list">
          {flowSteps.map((step, index) => (
            <li key={step}>
              <span>{String(index + 1).padStart(2, '0')}</span>
              <strong>{step}</strong>
            </li>
          ))}
        </ol>
      </section>

      <section className="about-cta-panel">
        <div className="about-cta-icon" aria-hidden="true">
          <Route size={24} />
          <Leaf size={16} />
        </div>
        <div>
          <h2>Built for clearer agricultural operations.</h2>
          <p>AgriAssist brings planning, field activities, resources and approvals into one coordinated operational experience.</p>
        </div>
        <Link className="ui-button ui-button-primary about-cta-button" to="/login">
          <span>Staff Login</span>
          <ArrowRight size={16} aria-hidden="true" />
        </Link>
      </section>
    </div>
  )
}