export type EvidenceStatus = 'Supported' | 'Partially Supported' | 'Unsupported' | 'Conflict' | 'Manual Review Required'
export type ReviewDecision = 'pending' | 'accepted' | 'edited' | 'rejected'

export type EvidenceProvenance = {
  sourceId: string
  sourceName: string
  organizationName: string
  originalUrl: string
  finalUrl: string
  sourceCategory: string
  country: string
  sourceClassification: 'Sri Lankan' | 'International fallback'
  stage: number
  evidenceText: string
  pageNumber?: number | null
  section?: string | null
}

export type DiscoveredSource = {
  sourceId: string
  title: string
  organizationName: string
  originalUrl: string
  finalUrl: string
  sourceCategory: string
  country: string
  sourceClassification: 'Sri Lankan' | 'International fallback'
  stage: number
  contentType?: string | null
  retrievalStatus: 'Retrieved' | 'Manual Review Required' | 'Failed'
  retrievedAt?: string | null
  acceptanceReason: string
  rejectionReason?: string | null
  manualReviewRequired: boolean
  pageCount?: number | null
  warnings: string[]
  existingReference: boolean
  existingReferenceId?: string | null
}

export type CropFindingSuggestion = {
  id: string
  name: string
  description?: string | null
  evidenceStatus: EvidenceStatus
  explanation: string
  provenance: EvidenceProvenance[]
  warnings: string[]
  alreadyExists: boolean
  existingCropTypeId?: string | null
  existingCropVarietyId?: string | null
}

type FindingSummary = {
  requestId: string
  usedInternationalFallback: boolean
  analysis: string[]
  recommendations: string[]
  warnings: string[]
}

export type CropSuggestionsResponse = FindingSummary & {
  action: 'SuggestCrops'
  sources: DiscoveredSource[]
  suggestions: CropFindingSuggestion[]
}

export type VarietySuggestionsResponse = FindingSummary & {
  action: 'SuggestVarieties'
  cropTypeId: string
  cropName: string
  sources: DiscoveredSource[]
  suggestions: CropFindingSuggestion[]
}

export type ReferenceDraftItem = {
  id: string
  field: 'sourceName' | 'sourceUrl' | 'sourceVersion' | 'region' | 'growthStage' | 'minimumDays' | 'maximumDays' | 'evidenceNotes' | 'structuredRule'
  suggestedValue: unknown
  displayValue: string
  evidenceStatus: EvidenceStatus
  explanation: string
  provenance: EvidenceProvenance[]
  warnings: string[]
  conflictGroupId?: string | null
}

export type ReferenceSourceDraft = {
  source: DiscoveredSource
  items: ReferenceDraftItem[]
  analysis: string[]
  recommendations: string[]
}

export type ReferenceDiscoveryResponse = FindingSummary & {
  action: 'DiscoverReferences'
  cropTypeId: string
  cropName: string
  cropVarietyId?: string | null
  varietyName?: string | null
  sourceDrafts: ReferenceSourceDraft[]
  unsupportedFields: string[]
}
