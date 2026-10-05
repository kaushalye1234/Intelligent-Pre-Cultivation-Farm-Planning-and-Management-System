import type { DiscoveredSource, EvidenceProvenance } from './cropFinding'

export type ResourceRequirementResearchStatus =
  | 'PendingVerification'
  | 'ConflictingSources'
  | 'NoVerifiedRecommendationFound'
  | 'EvidenceValidationFailed'

export type ResourceRequirementComponent = {
  label: string
  quantity: number
  evidenceText: string
}

export type ResourceRequirementRecommendation = {
  id: string
  quantityPerArea: number
  resourceUnit: string
  areaUnit: 'acre' | 'hectare'
  unitMatchesInventory: boolean
  basis: string
  components: ResourceRequirementComponent[]
  evidenceStatus: 'Supported' | 'Partially Supported'
  cropContext: string
  source: EvidenceProvenance
  warnings: string[]
}

/** Unverified draft from POST /resources/requirement-research. Nothing is saved until Verify & Save. */
export type ResourceRequirementResearchResponse = {
  requestId: string
  status: ResourceRequirementResearchStatus
  verified: false
  cropTypeId: string
  cropName: string
  cropVarietyId?: string | null
  varietyName?: string | null
  region?: string | null
  resourceId: string
  resourceName: string
  resourceUnit: string
  suggestedQuantityPerArea?: number | null
  suggestedResourceUnit?: string | null
  suggestedAreaUnit?: 'acre' | 'hectare' | null
  sourceName?: string | null
  sourceUrl?: string | null
  evidence?: string | null
  usedInternationalFallback: boolean
  recommendations: ResourceRequirementRecommendation[]
  rejectedClaims: string[]
  sources: DiscoveredSource[]
  warnings: string[]
}

export type VerifiedResourceRequirement = {
  cropReferenceProfileId: string
  ruleId: string
  ruleKey: string
  cropTypeId: string
  varietyName?: string | null
  region?: string | null
  resourceId: string
  resourceName: string
  quantityPerArea: number
  resourceUnit: string
  areaUnit: 'acre' | 'hectare'
  sourceName: string
  sourceUrl: string
  verifiedAt: string
  createdReferenceProfile: boolean
  replacedPreviousRule: boolean
}
