import { ArrowRight, BrainCircuit, Database, Layers, MonitorSmartphone, Server, ShieldCheck, Smartphone } from 'lucide-react'
import { Link } from 'react-router-dom'
import { capabilities } from './publicContent'

const missionPoints = [
  { title: 'Simplify agricultural planning', description: 'Turn crop plan requests into structured, trackable seasonal workflows.' },
  { title: 'Manage farm resources', description: 'Keep inventory, suppliers and reservations accurate and visible to the right staff.' },
  { title: 'Support planning and monitoring', description: 'Connect field inspections, crop issues and follow-ups to the plans they affect.' },
  { title: 'Bring information together', description: 'Put weather, field evidence, resources and approvals in one shared platform.' },
]

const technology = [
  {
    title: 'Web staff portal',
    description: 'React and TypeScript application for officers and administrators, built with Vite.',
    icon: MonitorSmartphone,
  },
  {
    title: 'Mobile app',
    description: 'Flutter application for the farmer-facing experience.',
    icon: Smartphone,
  },
  {
    title: 'Core API',
    description: 'ASP.NET Core 8 API with role-based authorisation, validation and audit-aware business services.',
    icon: Server,
  },
  {
    title: 'Data & integrations',
    description: 'PostgreSQL on Supabase via EF Core, OpenWeatherMap forecasts and Cloudinary image storage.',
    icon: Database,
  },
  {
    title: 'AI service',
    description: 'FastAPI and LangGraph agents — planning coordinator, field analysis, weather & resource, and scheduling validation.',
    icon: BrainCircuit,
  },
  {
    title: 'Human-in-the-loop',
    description: 'AI output is recorded as a reviewable snapshot; officers approve before any task or schedule is created.',
    icon: ShieldCheck,
  },
]

const modules = [
  'Crop & season planning',
  'Field inspections & crop issues',
  'Resources, inventory & weather',
  'Farm tasks, irrigation & approvals',
]

const reasons = [
  {
    title: 'Fewer disconnected handoffs',
    description: 'Farm work crosses several people and records. A shared view means each step starts from the same information.',
  },
  {
    title: 'Decisions stay accountable',
    description: 'Approvals, rejections and revision requests are recorded against the workflow they belong to.',
  },
  {
    title: 'Checks happen at approval time',
    description: 'Inventory and scheduling constraints are rechecked when an officer approves, not only when analysis ran.',
  },
]

export function AboutPage() {
  return (
    <div className="public-page about-page">
      <section className="public-hero public-hero-compact" aria-labelledby="about-title">
        <div className="public-container public-hero-narrow">
          <p className="public-eyebrow"><span aria-hidden="true" />About AgriAssist</p>
          <h1 id="about-title">
            One platform for planning, field operations, resources <span>and approvals.</span>
          </h1>
          <p className="public-lead">
            AgriAssist is an intelligent pre-cultivation farm planning and management system. It brings crop planning,
            field inspections, resource management, weather information and task approvals together so agricultural
            teams can prepare each season with clearer information.
          </p>
        </div>
      </section>

      <section className="public-section" aria-labelledby="about-mission-title">
        <div className="public-container about-split">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Our mission</p>
            <h2 id="about-mission-title">Make pre-cultivation planning clearer and easier to coordinate</h2>
            <p>
              Preparing a season involves farmers, field officers, resource officers and agricultural officers.
              AgriAssist gives them one place to plan, check and agree on the work before it reaches the field.
            </p>
          </header>
          <ul className="mission-list">
            {missionPoints.map((point, index) => (
              <li key={point.title}>
                <span className="mission-index">{String(index + 1).padStart(2, '0')}</span>
                <div>
                  <strong>{point.title}</strong>
                  <p>{point.description}</p>
                </div>
              </li>
            ))}
          </ul>
        </div>
      </section>

      <section className="public-section public-section-muted" aria-labelledby="about-capabilities-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Core capabilities</p>
            <h2 id="about-capabilities-title">What the platform does today</h2>
          </header>
          <div className="capability-grid">
            {capabilities.map(({ title, description, icon: Icon }) => (
              <article className="capability-card" key={title}>
                <span className="capability-icon"><Icon size={20} aria-hidden="true" /></span>
                <h3>{title}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="public-section" aria-labelledby="about-technology-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Technology</p>
            <h2 id="about-technology-title">An intelligent system with people in control</h2>
            <p>
              AgriAssist combines a conventional business platform with an AI workflow service. The agents analyse and
              propose; the API validates, records and enforces the approval rules.
            </p>
          </header>
          <div className="tech-grid">
            {technology.map(({ title, description, icon: Icon }) => (
              <article className="tech-item" key={title}>
                <span className="capability-icon"><Icon size={18} aria-hidden="true" /></span>
                <div>
                  <h3>{title}</h3>
                  <p>{description}</p>
                </div>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="public-section public-section-muted" aria-labelledby="about-project-title">
        <div className="public-container about-split">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Project</p>
            <h2 id="about-project-title">A university software engineering project</h2>
            <p>
              AgriAssist is developed as a team project. Each team member owns one functional module and its AI agent,
              and the modules connect through a shared data model and a single end-to-end workflow.
            </p>
          </header>
          <div className="module-panel">
            <span className="module-panel-label"><Layers size={16} aria-hidden="true" />Project modules</span>
            <ol>
              {modules.map((module, index) => (
                <li key={module}>
                  <span>Module {index + 1}</span>
                  <strong>{module}</strong>
                </li>
              ))}
            </ol>
          </div>
        </div>
      </section>

      <section className="public-section" aria-labelledby="about-why-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Why AgriAssist</p>
            <h2 id="about-why-title">Practical by design</h2>
          </header>
          <div className="reason-grid">
            {reasons.map((reason) => (
              <article className="reason-card" key={reason.title}>
                <h3>{reason.title}</h3>
                <p>{reason.description}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="public-section public-section-last" aria-labelledby="about-cta-title">
        <div className="public-container">
          <div className="public-cta-band">
            <div>
              <h2 id="about-cta-title">Explore the staff portal</h2>
              <p>Sign in to work with crop plans, inspections, resources and approvals for your role.</p>
            </div>
            <div className="public-actions">
              <Link className="ui-button ui-button-primary public-button-lg" to="/login">
                <span>Staff Login</span>
                <ArrowRight size={17} aria-hidden="true" />
              </Link>
              <Link className="ui-button ui-button-secondary public-button-lg public-button-inverse" to="/">
                <span>Back to Home</span>
              </Link>
            </div>
          </div>
        </div>
      </section>
    </div>
  )
}
