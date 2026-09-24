import type { ApplicationRole } from './types'

export const roleLabels: Record<ApplicationRole, string> = {
  1: 'Farmer',
  2: 'Field Officer',
  3: 'Resource Officer',
  4: 'Agricultural Officer',
  5: 'Admin',
}

export const cropPlanStatus: Record<number, string> = {
  1: 'Draft',
  2: 'Submitted',
  3: 'Preliminary',
  4: 'Approved',
  5: 'Rejected',
  6: 'Cancelled',
}

export const inspectionStatus: Record<number, string> = {
  1: 'Scheduled',
  2: 'In progress',
  3: 'Completed',
  4: 'Escalated',
  5: 'Cancelled',
}

export const issueSeverity: Record<number, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

export const issueStatus: Record<number, string> = {
  1: 'Open',
  2: 'Escalated',
  3: 'Resolved',
  4: 'Closed',
}

export const taskStatus: Record<number, string> = {
  1: 'Draft',
  2: 'Pending',
  3: 'Approved',
  4: 'Rejected',
  5: 'Revision',
  6: 'Completed',
  7: 'Cancelled',
}

export const scheduleStatus: Record<number, string> = {
  1: 'Pending',
  2: 'Approved',
  3: 'Rejected',
  4: 'Revision',
  5: 'Completed',
  6: 'Cancelled',
}

export const reservationStatus: Record<number, string> = {
  1: 'Reserved',
  2: 'Released',
  3: 'Cancelled',
}

export const approvalDecision: Record<number, string> = {
  1: 'Approved',
  2: 'Rejected',
  3: 'Revision Requested',
  4: 'Cancelled',
}

export const stockTransactionType: Record<number, string> = {
  1: 'Stock added',
  2: 'Stock removed',
  3: 'Reserved',
  4: 'Released',
}
