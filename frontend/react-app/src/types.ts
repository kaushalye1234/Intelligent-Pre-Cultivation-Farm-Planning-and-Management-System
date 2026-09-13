export type ApplicationRole = 1 | 2 | 3 | 4 | 5

export type UserProfile = {
  id: string
  fullName: string
  email: string
  role: ApplicationRole
  isActive: boolean
}

export type AuthResponse = {
  accessToken: string
  expiresAt: string
  user: UserProfile
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type DashboardSummary = {
  usersByRole: { role: string; count: number }[]
  activeFarms: number
  activeCropPlans: number
  openCropIssues: number
  lowStockResources: number
  pendingTasks: number
  pendingApprovals: number
}

export type Farm = {
  id: string
  name: string
  location: string
  totalArea: number
  ownerUserId: string
  createdAt: string
}

export type Field = {
  id: string
  farmId: string
  name: string
  area: number
  soilType: string
  isActive: boolean
}

export type CropType = {
  id: string
  name: string
  description?: string
  isActive: boolean
}

export type CropPlan = {
  id: string
  farmId: string
  fieldId?: string
  cropTypeId: string
  preferredStartDate: string
  preferredEndDate: string
  budget: number
  objective: string
  status: number
  createdAt: string
}

export type Inspection = {
  id: string
  fieldId: string
  inspectorUserId: string
  scheduledAt: string
  completedAt?: string
  status: number
  summary: string
}

export type Observation = {
  id: string
  fieldInspectionId: string
  observationType: string
  notes: string
}

export type CropIssue = {
  id: string
  fieldInspectionId: string
  title: string
  description: string
  severity: number
  status: number
  escalatedAt?: string
}

export type InspectionImage = {
  id: string
  fieldInspectionId: string
  url: string
  publicId: string
  contentType: string
  sizeBytes: number
}

export type FollowUpRecommendation = {
  id: string
  cropIssueId: string
  recommendation: string
  dueAt?: string
  isCompleted: boolean
}

export type InspectionDetail = Inspection & {
  observations: Observation[]
  issues: CropIssue[]
  images: InspectionImage[]
}

export type InspectionHistoryEvent = {
  occurredAt: string
  eventType: string
  summary: string
  relatedId?: string
}

export type FieldAnalysisFieldCondition = {
  summary: string
  evidenceInspectionIds: string[]
}

export type FieldAnalysisOpenIssue = {
  issueId: string
  severity: string
  status: string
  evidenceInspectionId?: string
}

export type FieldAnalysisResult = {
  workflowId: string
  status: string
  requiresHumanReview: boolean
  warnings: string[]
  fieldCondition: FieldAnalysisFieldCondition
  openIssues: FieldAnalysisOpenIssue[]
  priority: string
}
export type ResourceCategory = {
  id: string
  name: string
  description?: string
}

export type Supplier = {
  id: string
  name: string
  contactEmail: string
  phone: string
}

export type ResourceItem = {
  id: string
  resourceCategoryId: string
  supplierId?: string
  name: string
  unit: string
  isActive: boolean
}

export type InventoryStock = {
  id: string
  resourceId: string
  quantityOnHand: number
  reservedQuantity: number
  availableQuantity: number
  lowStockThreshold: number
}

export type Reservation = {
  id: string
  inventoryStockId: string
  requestedByUserId: string
  quantity: number
  status: number
  releasedAt?: string
  purpose: string
}

export type FarmTask = {
  id: string
  farmId: string
  title: string
  description: string
  dueAt: string
  assignedToUserId: string
  status: number
}

export type IrrigationSchedule = {
  id: string
  fieldId: string
  scheduledAt: string
  durationMinutes: number
  notes: string
  status: number
}

export type ApprovalDecision = {
  id: string
  farmTaskId?: string
  irrigationScheduleId?: string
  decidedByUserId: string
  agentWorkflowId?: string
  decision: number
  comment: string
  createdAt: string
}

export type AgentStepStatus = {
  id: string
  agentName: string
  stepName: string
  sequence: number
  status: number
  startedAt?: string
  completedAt?: string
  errorCode?: string
  errorMessageSafe?: string
}

export type CropPlanningWorkflowStatus = {
  workflowId: string
  cropPlanRequestId: string
  status: number
  currentStep: string
  createdAt: string
  completedAt?: string
  steps: AgentStepStatus[]
  warnings: string[]
}

export type CropPlanningDelegatedStep = {
  sequence: number
  stepType: string
  assignedAgent: string
}

export type CropPlanningResult = {
  workflowId: string
  status: string
  requiresHumanReview: boolean
  warnings: string[]
  referenceDataStatus: string
  objectiveSummary: string
  steps: CropPlanningDelegatedStep[]
}

export type CropPlanningWorkflowStart = {
  workflowId: string
  cropPlanRequestId: string
  coordinatorStepId: string
  status: string
  requiresHumanReview: boolean
  warnings: string[]
}


