export type ProposalSource = {
  kind: string
  id: string
  label: string
  sourceUrl?: string
}

type ExplainedItem = { reason: string; sources: ProposalSource[] }
export type ProposalTask = ExplainedItem & { title: string; dueAt: string }
export type ProposalIrrigation = ExplainedItem & { scheduledAt: string; durationMinutes: number }
export type ProposalReservation = ExplainedItem & { inventoryStockId: string; quantity: number }
export type CropHealthProposalTask = {
  actionKey: string
  actionType: string
  taskCategory: string
  title: string
  description: string
  timingCategory: string
  dueAt: string
  assignedToUserId: string
  included: boolean
  schedulingNote?: string
}
export type CropHealthGuidanceProposal = {
  cropHealthObservation: string
  possibleConcern: string
  uncertaintyGuidance: string
  prePlantingActions: string[]
  monitoringActions: string[]
  escalationGuidance?: string
  whyThisIsRecommended: string
  decision: 'PendingDecision' | 'Included' | 'Rejected' | 'NotApplicable'
  rejectionReason?: string
}

export type SchedulingProposal = {
  status: 'CandidateReady' | 'CandidateBlocked'
  warnings: string[]
  blocking: string[]
  tasks: ProposalTask[]
  irrigation: ProposalIrrigation[]
  reservations: ProposalReservation[]
  cropHealthTasks: CropHealthProposalTask[]
  cropHealthGuidance: CropHealthGuidanceProposal | null
}

function record(value: unknown): Record<string, unknown> | null {
  return value !== null && typeof value === 'object' && !Array.isArray(value)
    ? value as Record<string, unknown> : null
}

function sources(value: unknown): ProposalSource[] | null {
  if (!Array.isArray(value) || value.length < 1 || value.length > 4) return null
  const parsed: ProposalSource[] = []
  for (const entry of value) {
    const item = record(entry)
    if (!item || typeof item.kind !== 'string' || typeof item.id !== 'string' ||
        typeof item.label !== 'string' || !item.label.trim()) return null
    parsed.push({ kind: item.kind, id: item.id, label: item.label,
      sourceUrl: typeof item.sourceUrl === 'string' ? item.sourceUrl : undefined })
  }
  return parsed
}

function explained(value: unknown): (ExplainedItem & Record<string, unknown>) | null {
  const item = record(value)
  const itemSources = sources(item?.sources)
  if (!item || typeof item.reason !== 'string' || !item.reason.trim() || !itemSources) return null
  return { ...item, reason: item.reason, sources: itemSources }
}

export function safeSourceUrl(value?: string): string | null {
  if (!value) return null
  try {
    const url = new URL(value)
    return url.protocol === 'https:' || url.protocol === 'http:' ? url.href : null
  } catch {
    return null
  }
}

export function parseSchedulingOutput(value: unknown): SchedulingProposal | null {
  const output = record(value)
  if (!output || output.contractVersion !== 2 ||
      (output.status !== 'CandidateReady' && output.status !== 'CandidateBlocked') ||
      !Array.isArray(output.candidateTasks) || !Array.isArray(output.candidateIrrigation) ||
      !Array.isArray(output.candidateReservations)) return null
  const tasks: ProposalTask[] = []
  const irrigation: ProposalIrrigation[] = []
  const reservations: ProposalReservation[] = []
  const cropHealthTasks: CropHealthProposalTask[] = []
  for (const value of output.candidateTasks) {
    const item = explained(value)
    if (!item || typeof item.title !== 'string' || typeof item.dueAt !== 'string') return null
    tasks.push({ title: item.title, dueAt: item.dueAt, reason: item.reason, sources: item.sources })
  }
  for (const value of output.candidateIrrigation) {
    const item = explained(value)
    if (!item || typeof item.scheduledAt !== 'string' || typeof item.durationMinutes !== 'number') return null
    irrigation.push({ scheduledAt: item.scheduledAt, durationMinutes: item.durationMinutes,
      reason: item.reason, sources: item.sources })
  }
  for (const value of output.candidateReservations) {
    const item = explained(value)
    if (!item || typeof item.inventoryStockId !== 'string' || typeof item.quantity !== 'number') return null
    reservations.push({ inventoryStockId: item.inventoryStockId, quantity: item.quantity,
      reason: item.reason, sources: item.sources })
  }
  const rawCropHealthTasks = output.cropHealthCandidateTasks ?? []
  if (!Array.isArray(rawCropHealthTasks)) return null
  for (const value of rawCropHealthTasks) {
    const item = record(value)
    if (!item || typeof item.actionKey !== 'string' || typeof item.actionType !== 'string' ||
      typeof item.taskCategory !== 'string' || typeof item.title !== 'string' ||
      typeof item.description !== 'string' || typeof item.timingCategory !== 'string' ||
      typeof item.dueAt !== 'string' || typeof item.assignedToUserId !== 'string' ||
      typeof item.included !== 'boolean') return null
    cropHealthTasks.push({
      actionKey: item.actionKey,
      actionType: item.actionType,
      taskCategory: item.taskCategory,
      title: item.title,
      description: item.description,
      timingCategory: item.timingCategory,
      dueAt: item.dueAt,
      assignedToUserId: item.assignedToUserId,
      included: item.included,
      schedulingNote: typeof item.schedulingNote === 'string' ? item.schedulingNote : undefined,
    })
  }
  let cropHealthGuidance: CropHealthGuidanceProposal | null = null
  if (output.cropHealthGuidance != null) {
    const guidance = record(output.cropHealthGuidance)
    const decisions = ['PendingDecision', 'Included', 'Rejected', 'NotApplicable']
    if (!guidance || typeof guidance.cropHealthObservation !== 'string' || typeof guidance.possibleConcern !== 'string' ||
      typeof guidance.uncertaintyGuidance !== 'string' || !Array.isArray(guidance.prePlantingActions) ||
      !guidance.prePlantingActions.every((item) => typeof item === 'string') || !Array.isArray(guidance.monitoringActions) ||
      !guidance.monitoringActions.every((item) => typeof item === 'string') || typeof guidance.whyThisIsRecommended !== 'string' ||
      typeof guidance.decision !== 'string' || !decisions.includes(guidance.decision)) return null
    cropHealthGuidance = {
      cropHealthObservation: guidance.cropHealthObservation,
      possibleConcern: guidance.possibleConcern,
      uncertaintyGuidance: guidance.uncertaintyGuidance,
      prePlantingActions: guidance.prePlantingActions as string[],
      monitoringActions: guidance.monitoringActions as string[],
      escalationGuidance: typeof guidance.escalationGuidance === 'string' ? guidance.escalationGuidance : undefined,
      whyThisIsRecommended: guidance.whyThisIsRecommended,
      decision: guidance.decision as CropHealthGuidanceProposal['decision'],
      rejectionReason: typeof guidance.rejectionReason === 'string' ? guidance.rejectionReason : undefined,
    }
  }
  const warnings = Array.isArray(output.warnings) ? output.warnings.filter((item): item is string => typeof item === 'string') : []
  const constraints = Array.isArray(output.constraints) ? output.constraints : []
  const blocking = constraints.flatMap((entry) => {
    const item = record(entry)
    return item?.severity === 'Blocking' && item.code !== 'HUMAN_APPROVAL' && item.code !== 'DATE_WINDOW' &&
      typeof item.message === 'string' ? [item.message] : []
  })
  return { status: output.status, warnings, blocking, tasks, irrigation, reservations, cropHealthTasks, cropHealthGuidance }
}
