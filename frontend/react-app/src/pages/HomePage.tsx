import { ArrowRight, CheckCircle2, ShieldCheck, UserRound } from 'lucide-react'
import { Link } from 'react-router-dom'
import { capabilities, roleSummaries, workflowSteps } from './publicContent'

export function HomePage() {
  return (
    <div className="public-page home-page" data-testid="home-page">
      <section className="public-hero" aria-labelledby="home-hero-title">
        <div className="public-container public-hero-grid">
          <div className="public-hero-copy">
            <p className="public-eyebrow"><span aria-hidden="true" />AgriAssist AI</p>
            <h1 id="home-hero-title">
              Smart Agricultural Planning <span>&amp; Farm Management</span>
            </h1>
            <p className="public-lead">
              AgriAssist AI helps farmers and agricultural teams make better planning and resource decisions through
              intelligent, data-driven workflows — from the first crop plan to an approved field schedule.
            </p>
            <div className="public-actions">
              <Link className="ui-button ui-button-primary public-button-lg" to="/login">
                <span>Staff Login</span>
                <ArrowRight size={17} aria-hidden="true" />
              </Link>
              <Link className="ui-button ui-button-secondary public-button-lg" to="/about">
                <span>Learn More</span>
              </Link>
            </div>
            <ul className="public-hero-points" aria-label="Platform principles">
              <li><CheckCircle2 size={16} aria-hidden="true" />Role-based staff portal</li>
              <li><CheckCircle2 size={16} aria-hidden="true" />AI-assisted analysis</li>
              <li><CheckCircle2 size={16} aria-hidden="true" />Human approval on every plan</li>
            </ul>
          </div>

          <div className="hero-preview" aria-hidden="true">
            <div className="hero-preview-header">
              <span className="hero-preview-dots"><i /><i /><i /></span>
              <strong>Crop planning workflow</strong>
            </div>
            <ol className="hero-preview-steps">
              {workflowSteps.map((step, index) => (
                <li key={step.phase}>
                  <span className="hero-preview-index">{index + 1}</span>
                  <div>
                    <small>{step.phase}</small>
                    <strong>{step.title}</strong>
                  </div>
                  {index === workflowSteps.length - 1
                    ? <span className="hero-preview-tag hero-preview-tag-review">Human review</span>
                    : <span className="hero-preview-tag">Step {index + 1}</span>}
                </li>
              ))}
            </ol>
            <div className="hero-preview-footer">
              <ShieldCheck size={16} />
              <span>No task, schedule or reservation is final until an officer approves it.</span>
            </div>
          </div>
        </div>
      </section>

      <section className="public-section" aria-labelledby="home-features-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />Platform capabilities</p>
            <h2 id="home-features-title">Built around real agricultural workflows</h2>
            <p>Each module maps to a part of the pre-cultivation process, and all of them share the same farms, fields and records.</p>
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

      <section className="public-section public-section-muted" aria-labelledby="home-process-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />How it works</p>
            <h2 id="home-process-title">From request to approval</h2>
            <p>A crop plan moves through analysis and preparation before anything is scheduled in the field.</p>
          </header>
          <ol className="workflow-steps" aria-label="Request to approval workflow">
            {workflowSteps.map((step, index) => (
              <li key={step.phase}>
                <span className="workflow-step-number">{String(index + 1).padStart(2, '0')}</span>
                <small>{step.phase}</small>
                <strong>{step.title}</strong>
                <p>{step.description}</p>
              </li>
            ))}
          </ol>
          <div className="public-callout">
            <ShieldCheck size={20} aria-hidden="true" />
            <div>
              <strong>AI assists. People decide.</strong>
              <p>
                The AI workflow produces analysis, warnings and candidate tasks. Final tasks, irrigation schedules and
                inventory reservations are only created after an authorised officer approves them.
              </p>
            </div>
          </div>
        </div>
      </section>

      <section className="public-section" aria-labelledby="home-roles-title">
        <div className="public-container">
          <header className="public-section-header">
            <p className="public-eyebrow"><span aria-hidden="true" />User roles</p>
            <h2 id="home-roles-title">Clear responsibilities across the farm operation</h2>
            <p>Role-based access keeps each person focused on their own work while the wider operation stays connected.</p>
          </header>
          <div className="role-grid">
            {roleSummaries.map((role) => (
              <article className="role-tile" key={role.title}>
                <span className="role-tile-icon"><UserRound size={18} aria-hidden="true" /></span>
                <h3>{role.title}</h3>
                <p>{role.description}</p>
              </article>
            ))}
          </div>
        </div>
      </section>

      <section className="public-section public-section-last" aria-labelledby="home-cta-title">
        <div className="public-container">
          <div className="public-cta-band">
            <div>
              <h2 id="home-cta-title">Ready to plan the next season?</h2>
              <p>Sign in to the AgriAssist staff portal to manage crop plans, inspections, resources and approvals.</p>
            </div>
            <div className="public-actions">
              <Link className="ui-button ui-button-primary public-button-lg" to="/login">
                <span>Staff Login</span>
                <ArrowRight size={17} aria-hidden="true" />
              </Link>
              <Link className="ui-button ui-button-secondary public-button-lg public-button-inverse" to="/about">
                <span>About AgriAssist</span>
              </Link>
            </div>
          </div>
        </div>
      </section>
    </div>
  )
}
