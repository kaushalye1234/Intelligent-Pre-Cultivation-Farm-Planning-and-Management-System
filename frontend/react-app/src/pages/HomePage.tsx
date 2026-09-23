import { ArrowRight, CalendarCheck, ClipboardCheck, PackageCheck, ShieldCheck, Sprout, Users } from 'lucide-react'
import { Link } from 'react-router-dom'
import { HomeGrowthPlant } from './HomeGrowthPlant'
import styles from './HomePage.module.css'
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
  const pageRef = useHomeScrollReveal(styles.revealReady, styles.revealed)

  return (
    <div ref={pageRef} className={styles.page} data-testid="home-page">
      <section className={styles.hero} aria-labelledby="home-hero-title">
        <div className={styles.heroInner}>
          <div className={styles.heroCopy} data-home-reveal>
            <p className={styles.eyebrow}><span aria-hidden="true" />AgriAssist</p>
            <h1 id="home-hero-title">
              Smart agricultural planning, <em>rooted in real farm operations.</em>
            </h1>
            <p className={styles.heroSummary}>
              AgriAssist helps farmers and agricultural staff coordinate crop planning, field inspections, resources,
              schedules and manual approvals through a connected operations platform.
            </p>
            <div className={styles.heroActions}>
              <Link className={styles.primaryAction} to="/about">
                Learn More <ArrowRight size={17} aria-hidden="true" />
              </Link>
              <Link className={styles.secondaryAction} to="/login">Staff Login</Link>
            </div>
            <div className={styles.assurance} aria-hidden="true">
              <span>Plan</span><span>Coordinate</span><span>Approve</span>
            </div>
          </div>

          <div className={styles.heroVisual} aria-hidden="true" data-home-reveal>
            <div className={styles.fieldSun} />
            <div className={styles.fieldHorizon} />
            <div className={styles.fieldPlot}>
              <span /><span /><span /><span />
            </div>
            <div className={styles.heroCrop}>
              <svg viewBox="0 0 180 280" focusable="false">
                <path data-crop-stem d="M91 261 C87 218 96 176 89 134 C84 101 91 68 89 34" />
                <path data-crop-leaf d="M89 198 C61 190 46 168 48 140 C76 143 96 165 89 198 Z" />
                <path data-crop-vein d="M87 195 C73 173 61 157 51 146" />
                <path data-crop-leaf d="M91 173 C113 157 137 159 150 178 C132 198 107 198 91 173 Z" />
                <path data-crop-vein d="M95 173 C116 176 131 178 145 179" />
                <path data-crop-leaf d="M88 121 C67 112 57 93 60 72 C83 76 96 94 88 121 Z" />
                <path data-crop-leaf d="M90 92 C108 78 128 80 139 95 C124 112 104 112 90 92 Z" />
                <g data-crop-head>
                  <path d="M89 48 C76 32 78 15 89 4 C100 16 102 33 89 48 Z" />
                  <ellipse cx="74" cy="39" rx="6" ry="14" transform="rotate(-30 74 39)" />
                  <ellipse cx="104" cy="39" rx="6" ry="14" transform="rotate(30 104 39)" />
                </g>
              </svg>
            </div>
            <div className={`${styles.fieldMarker} ${styles.markerOne}`}><Sprout size={17} /></div>
            <div className={`${styles.fieldMarker} ${styles.markerTwo}`}><ClipboardCheck size={17} /></div>
            <div className={`${styles.fieldMarker} ${styles.markerThree}`}><ShieldCheck size={17} /></div>
            <div className={styles.visualCaption}>
              <span>One connected journey</span>
              <strong>From field insight to confident action</strong>
            </div>
          </div>
        </div>
      </section>

      <section className={styles.section} aria-labelledby="home-features-title">
          <div className={styles.sectionHeader} data-home-reveal>
            <div>
              <p className={styles.sectionEyebrow}>Core Features</p>
              <h2 id="home-features-title">Built around real agricultural workflows</h2>
            </div>
            <p className={styles.sectionIntro}>
              One connected workspace supports every stage of cultivation while keeping people responsible for each decision.
            </p>
          </div>
          <div className={styles.featureGrid}>
            {features.map((feature, index) => {
              const Icon = feature.icon
              return (
                <article className={styles.featureCard} key={feature.title} data-home-reveal>
                  <div className={styles.cardTopline}>
                    <span>0{index + 1}</span>
                    <div className={styles.featureIcon}><Icon size={22} aria-hidden="true" /></div>
                  </div>
                  <h3>{feature.title}</h3>
                  <p>{feature.description}</p>
                  <span className={styles.cardDetail} aria-hidden="true">
                    Connected operation <ArrowRight size={14} />
                  </span>
                </article>
              )
            })}
          </div>
      </section>

      <section className={styles.workflowSection} aria-labelledby="home-process-title">
        <div className={styles.workflowHeader} data-home-reveal>
          <p className={styles.sectionEyebrow}>How It Works</p>
          <h2 id="home-process-title">From request to approval</h2>
          <p>Watch the cultivation spine grow as the operational plan moves from an initial request to a reviewed decision.</p>
        </div>

        <div className={styles.workflowGrid}>
          <HomeGrowthPlant />
          <div className={styles.workflowContent}>
            <ol className={styles.processList} aria-label="Request to approval workflow" data-home-reveal>
              {processSteps.map(([phaseName, step], index) => (
                <li key={step}>
                  <span className={styles.processNumber}>0{index + 1}</span>
                  <div>
                    <small>{phaseName}</small>
                    <strong>{step}</strong>
                  </div>
                  <ArrowRight size={16} aria-hidden="true" />
                </li>
              ))}
            </ol>

            <div className={styles.projectNote} data-home-reveal>
              <Sprout size={20} aria-hidden="true" />
              <div>
                <strong>AI-ready foundation</strong>
                <p>The platform is designed with an AI-ready architecture for future intelligent planning support. Real Agentic AI is not active in this phase.</p>
              </div>
            </div>
          </div>
        </div>
      </section>

      <section className={styles.section} aria-labelledby="home-roles-title">
          <div className={styles.sectionHeader} data-home-reveal>
            <div>
              <p className={styles.sectionEyebrow}>User Roles</p>
              <h2 id="home-roles-title">Clear responsibilities across the farm operation</h2>
            </div>
            <p className={styles.sectionIntro}>
              Role-aware access keeps each team focused while the wider agricultural operation stays connected.
            </p>
          </div>
          <div className={styles.roleGrid}>
            {roles.map(([title, description], index) => (
              <article className={styles.roleCard} key={title} data-home-reveal>
                <div className={styles.roleCardHeader}>
                  <div className={styles.roleIcon}><Users size={18} aria-hidden="true" /></div>
                  <span>Role 0{index + 1}</span>
                </div>
                <h3>{title}</h3>
                <p>{description}</p>
              </article>
            ))}
          </div>
      </section>

      <section className={styles.cta} aria-labelledby="home-cta-title" data-home-reveal>
        <div className={styles.ctaIcon}><ShieldCheck size={25} aria-hidden="true" /></div>
        <div className={styles.ctaCopy}>
          <span>Secure staff access</span>
          <h2 id="home-cta-title">Staff member?</h2>
          <p>Sign in to access your AgriAssist operations dashboard.</p>
        </div>
        <Link className={styles.ctaAction} to="/login">
          Staff Login <ArrowRight size={17} aria-hidden="true" />
        </Link>
      </section>
    </div>
  )
}
