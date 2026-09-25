import { BrainCircuit, CalendarCheck, ClipboardCheck, CloudSun, PackageCheck, Sprout } from 'lucide-react'
import type { LucideIcon } from 'lucide-react'

/*
 * Copy shared by the public Home and About pages. Every item describes a capability that exists in
 * the current AgriAssist web, API or AI-service code; do not add claims the system cannot back up.
 */

export type Capability = {
  title: string
  description: string
  icon: LucideIcon
}

export const capabilities: Capability[] = [
  {
    title: 'Crop & Season Planning',
    description: 'Manage farms, fields and crop types, and track crop plan requests through their seasonal planning workflow.',
    icon: Sprout,
  },
  {
    title: 'Field Inspections & Crop Issues',
    description: 'Schedule inspections, record pre-planting field assessments, and follow crop issues, escalations and follow-ups.',
    icon: ClipboardCheck,
  },
  {
    title: 'Resource & Inventory Management',
    description: 'Maintain resource catalogues, categories, suppliers and stock levels, with reservations tied to real availability.',
    icon: PackageCheck,
  },
  {
    title: 'Weather Forecasts',
    description: 'Look up a multi-day forecast for a farm location — temperature, rainfall and wind — alongside resource decisions.',
    icon: CloudSun,
  },
  {
    title: 'Tasks, Irrigation & Approvals',
    description: 'Organise farm tasks and irrigation schedules, and record approve, reject and revision decisions by authorised staff.',
    icon: CalendarCheck,
  },
  {
    title: 'AI-Assisted Planning Workflow',
    description: 'Coordinated AI agents review a crop plan, field evidence, weather and resources, then propose tasks for human approval.',
    icon: BrainCircuit,
  },
]

export type WorkflowStep = {
  phase: string
  title: string
  description: string
}

/** The request-to-approval flow as implemented by the crop planning and task approval workflow. */
export const workflowSteps: WorkflowStep[] = [
  {
    phase: 'Plan',
    title: 'Crop plan request',
    description: 'A crop plan is created for a field and the planning coordinator checks it before analysis begins.',
  },
  {
    phase: 'Analyze',
    title: 'Field analysis',
    description: 'Field officers record pre-planting evidence that the field analysis step evaluates for readiness and risk.',
  },
  {
    phase: 'Manage Resources',
    title: 'Weather & resource review',
    description: 'Forecast conditions and inventory availability are checked against what the plan will need.',
  },
  {
    phase: 'Monitor',
    title: 'Inspections & crop issues',
    description: 'Inspections, crop issues and follow-up recommendations keep field conditions visible over the season.',
  },
  {
    phase: 'Act',
    title: 'Officer approval',
    description: 'Candidate tasks and irrigation schedules are validated, then approved, rejected or sent back by an officer.',
  },
]

export type RoleSummary = {
  title: string
  description: string
}

export const roleSummaries: RoleSummary[] = [
  { title: 'Farmer', description: 'Submits crop planning requests and works mainly through the mobile app.' },
  { title: 'Field Officer', description: 'Schedules inspections, records field observations and reports crop issues.' },
  { title: 'Resource Officer', description: 'Maintains resource catalogues, inventory levels and reservations.' },
  { title: 'Agricultural Officer', description: 'Reviews plans, tasks, schedules and approval decisions.' },
  { title: 'Admin', description: 'Manages staff access and oversees the full operations platform.' },
]
