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
