export type ApplicationRole = 1 | 2 | 3 | 4 | 5

export type UserProfile = {
  id: string
  fullName: string
  email: string
  role: ApplicationRole
  isActive: boolean
  mustChangePassword: boolean
}

export type AuthResponse = {
  authenticationStatus: 'authenticated' | 'passwordChangeRequired'
  accessToken: string | null
  accessTokenExpiresAt: string | null
  passwordChangeToken: string | null
  passwordChangeTokenExpiresAt: string | null
  user: UserProfile
}

export type LoginResult = {
  status: AuthResponse['authenticationStatus']
  user: UserProfile
}

export type AdminUser = UserProfile & {
  createdAt: string
  lastLoginAt?: string | null
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

export type CropVariety = {
  id: string
  cropTypeId: string
  name: string
  isActive: boolean
}

export type CropReferenceProfile = {
  id: string
  cropTypeId: string
  varietyName?: string | null
  region?: string | null
  sourceName: string
  sourceUrl?: string | null
  sourceVersion: string
  verifiedAt: string
  isActive: boolean
  stageCount: number
  ruleCount: number
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
  fieldSuitability?: 'Suitable' | 'SuitableWithConditions' | 'NotSuitable' | 'RequiresFurtherAssessment' | 'Unknown'
  soilAssessment?: string
  waterAssessment?: string
  drainageAssessment?: string
  fieldPreparationRequirements?: string[]
  plantingReadiness?: PrePlantingPlantingReadiness | 'Unknown'
  identifiedRisks?: PrePlantingRisk[]
  recommendedPrePlantingActions?: string[]
}

export type PrePlantingSoilType = 'Sandy' | 'Clay' | 'Loamy' | 'Silty' | 'Mixed' | 'Unknown' | 'Other'
export type PrePlantingSoilCondition = 'Good' | 'Moderate' | 'Poor' | 'Compacted' | 'Eroded' | 'Unknown' | 'Other'
export type PrePlantingSoilMoisture = 'Dry' | 'Moist' | 'Wet' | 'Waterlogged' | 'Unknown'
export type PrePlantingWaterAvailability = 'Adequate' | 'Limited' | 'Unavailable' | 'Seasonal' | 'Unknown'
export type PrePlantingIrrigationAvailability = 'Available' | 'Limited' | 'Unavailable' | 'NotRequired' | 'Unknown'
export type PrePlantingWaterReliability = 'Reliable' | 'Intermittent' | 'Seasonal' | 'Unreliable' | 'Unknown'
export type PrePlantingDrainageCondition = 'Good' | 'Moderate' | 'Poor' | 'Unknown'
export type PrePlantingWaterloggingRisk = 'NoneObserved' | 'Low' | 'Moderate' | 'High' | 'Unknown'
export type PrePlantingGeneralFieldCondition =
  | 'ClearAndPrepared'
  | 'RequiresLandPreparation'
  | 'UnevenField'
  | 'Waterlogged'
  | 'TooDry'
  | 'ErosionPresent'
  | 'AccessLimitation'
  | 'Other'
export type PrePlantingPlantingReadiness =
  | 'Ready'
  | 'ReadyWithMinorPreparation'
  | 'RequiresPreparation'
  | 'NotReady'
  | 'RequiresFurtherAssessment'
export type PrePlantingRisk =
  | 'WaterShortageRisk'
  | 'FloodingRisk'
  | 'PoorDrainage'
  | 'SoilSuitabilityConcern'
  | 'SoilErosion'
  | 'FieldAccessProblem'
  | 'LandPreparationRequired'
  | 'Other'

export type PrePlantingAssessmentInput = {
  soilType: PrePlantingSoilType | null
  soilCondition: PrePlantingSoilCondition | null
  soilMoisture: PrePlantingSoilMoisture | null
  soilNotes: string | null
  waterAvailability: PrePlantingWaterAvailability | null
  mainWaterSource: string | null
  irrigationAvailability: PrePlantingIrrigationAvailability | null
  waterReliability: PrePlantingWaterReliability | null
  waterConcerns: string | null
  drainageCondition: PrePlantingDrainageCondition | null
  waterloggingRisk: PrePlantingWaterloggingRisk | null
  drainageNotes: string | null
  generalFieldCondition: PrePlantingGeneralFieldCondition | null
  generalFieldNotes: string | null
  plantingReadiness: PrePlantingPlantingReadiness | null
  identifiedRisks: PrePlantingRisk[] | null
  riskNotes: string | null
  risksAndConcerns: string | null
  officerNotes: string | null
}

export type PrePlantingAssessmentImage = {
  id: string
  url: string
  contentType: string
  sizeBytes: number
}

export type PrePlantingAssessment = PrePlantingAssessmentInput & {
  inspectionId: string
  cropPlanRequestId: string
  fieldId: string
  inspectorUserId: string
  status: number
  scheduledAt: string
  completedAt: string | null
  images: PrePlantingAssessmentImage[]
}

export type PrePlantingContext = {
  cropPlanRequestId: string
  workflowId: string
  currentStep: string
  farmerId: string
  farmerName: string
  farmId: string
  farmName: string
  farmLocation: string
  fieldId: string
  fieldName: string
  cropTypeId: string
  cropName: string
  cropVarietyId: string | null
  cropVarietyName: string | null
  cultivationSeason: 0 | 1 | 2 | 3
  preferredStartDate: string
  preferredEndDate: string
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
  resourceName?: string
  unit?: string
}

// 1 Add, 2 Remove, 3 Reserve, 4 Release
export type StockTransaction = {
  id: string
  inventoryStockId: string
  type: number
  quantity: number
  note: string
  createdAt: string
}

export type Reservation = {
  id: string
  inventoryStockId: string
  requestedByUserId: string
  quantity: number
  status: number
  releasedAt?: string
  purpose: string
  resourceName?: string
  unit?: string
  createdAt?: string
}

export type WeatherDay = {
  date: string
  minTemperatureC: number
  maxTemperatureC: number
  rainMm: number
  maxWindSpeedMs: number
  description: string
}

export type WeatherForecast = {
  location: string
  isAvailable: boolean
  message: string
  days: WeatherDay[]
}

export type FarmTask = {
  id: string
  farmId: string
  title: string
  description: string
  dueAt: string
  assignedToUserId: string
  status: number
  generatedByWorkflowId?: string
  candidateRevision?: number
}

export type IrrigationSchedule = {
  id: string
  fieldId: string
  scheduledAt: string
  durationMinutes: number
  notes: string
  status: number
  generatedByWorkflowId?: string
  candidateRevision?: number
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

export type WorkflowSummary = {
  id: string
  cropPlanRequestId?: string
  objective: string
  status: number
  currentStep: string
  candidateRevision: number
  revisionCount: number
  version: number
  createdAt: string
  completedAt?: string
}

export type WorkflowStepReview = {
  id: string
  agentName: string
  stepName: string
  sequence: number
  candidateRevision: number
  status: number
  input: unknown
  output: unknown
  startedAt?: string
  completedAt?: string
  errorCode?: string
  errorMessageSafe?: string
}

export type WorkflowValidation = {
  id: string
  validatorName: string
  candidateRevision: number
  isValid: boolean
  errors: string[]
  warnings: string[]
  createdAt: string
}

export type WorkflowReview = {
  workflow: WorkflowSummary
  farmId: string
  fieldId?: string
  budget: number
  preferredStartDate: string
  preferredEndDate: string
  steps: WorkflowStepReview[]
  validations: WorkflowValidation[]
  decisions: ApprovalDecision[]
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


