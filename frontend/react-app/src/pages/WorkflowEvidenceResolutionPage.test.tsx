import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AuthContext } from '../auth/AuthContext'
import type { UserProfile } from '../types'
import { WorkflowEvidenceResolutionPage } from './WorkflowEvidenceResolutionPage'

const profile = { id: 'profile-1', cropTypeId: 'crop-1', varietyName: 'Bg 352', region: 'Anuradhapura',
  sourceName: 'Synthetic source', sourceUrl: 'https://example.test/rice', sourceVersion: 'test',
  verifiedAt: null, isActive: false, stageCount: 1, ruleCount: 1, verificationState: 1, draftVersion: 3 }
const details = { ...profile, cropName: 'Rice', stages: [{ id: 'stage-1', stageName: 'Planting', sequence: 1,
  typicalMinDays: 3, typicalMaxDays: 5, sourceName: profile.sourceName, sourceUrl: profile.sourceUrl }],
  rules: [{ id: 'rule-1', ruleType: 'ResourceRequirement', ruleKey: 'Urea', structuredValueJson: '{"quantityPerArea":1}',
    sourceName: profile.sourceName, sourceUrl: profile.sourceUrl }] }
const resolution = { workflowId: 'workflow-1', cropPlanRequestId: 'plan-1', cropTypeId: 'crop-1', cropName: 'Rice',
  cropVarietyId: 'variety-1', varietyName: 'Bg 352', fieldId: 'field-1', fieldName: 'Field A', region: 'Anuradhapura',
  status: 11, preferredStartDate: '2027-01-01', preferredEndDate: '2027-05-01', pinnedProfileId: 'profile-1',
  blockingReasons: ['Pinned profile has no growth stages.'], profiles: [profile], compatibleVerifiedProfileIds: [],
  successorWorkflowId: null, nextResponsibleRole: 'AgriculturalOfficer', canStartReplacement: false }

function show(role: UserProfile['role'] = 4) {
  render(<MemoryRouter initialEntries={['/task-approval/workflows/workflow-1/resolve-evidence']}>
    <AuthContext.Provider value={{ user: { id: 'officer-1', fullName: 'Officer', email: 'officer@example.test', role,
      isActive: true, mustChangePassword: false }, token: 'token', isAuthenticated: true, isLoading: false,
      passwordChangeUser: null, hasPasswordChangeSession: false, login: vi.fn(), changeTemporaryPassword: vi.fn(), logout: vi.fn() }}>
      <Routes><Route path="/task-approval/workflows/:id/resolve-evidence" element={<WorkflowEvidenceResolutionPage />} /></Routes>
    </AuthContext.Provider>
  </MemoryRouter>)
}
function reads(data: unknown = resolution, reference: unknown = details) {
  return vi.spyOn(api, 'get').mockImplementation(async (url) => {
    if (url.endsWith('/evidence-resolution')) return { data } as never
    if (url === '/crop-planning/crop-reference-profiles/profile-1') return { data: reference } as never
    throw new Error('Unexpected GET ' + url)
  })
}
afterEach(() => vi.restoreAllMocks())

describe('WorkflowEvidenceResolutionPage', () => {
  it('shows persisted blocking evidence and requires a separate officer confirmation', async () => {
    reads()
    show()
    expect(await screen.findByText('Pinned profile has no growth stages.')).toBeInTheDocument()
    expect(await screen.findByRole('link', { name: 'Synthetic source' })).toHaveAttribute('href', profile.sourceUrl)
    expect(screen.getByRole('button', { name: 'Verify and activate' })).toBeDisabled()
    expect(screen.queryByRole('button', { name: 'Start replacement workflow' })).not.toBeInTheDocument()
  })

  it('records the current draft version and observed regime through the verification endpoint', async () => {
    const user = userEvent.setup()
    reads()
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { ...details, verificationState: 2 } })
    show()
    await screen.findByRole('button', { name: 'Verify and activate' })
    await user.selectOptions(screen.getByLabelText(/^Field water regime/), '1')
    await user.type(screen.getByLabelText(/^Officer observation/), 'I inspected the canal and reviewed the source values.')
    await user.click(screen.getByLabelText(/I confirm the field regime/))
    await user.click(screen.getByRole('button', { name: 'Verify and activate' }))
    await waitFor(() => expect(post).toHaveBeenCalledWith('/crop-planning/crop-reference-profiles/profile-1/verify', {
      fieldId: 'field-1', waterRegime: 1, observation: 'I inspected the canal and reviewed the source values.',
      expectedDraftVersion: 3, confirmed: true,
    }))
    expect(await screen.findByText(/Reference verified and activated/)).toBeInTheDocument()
  })

  it('keeps a rules-only draft blocked and surfaces backend conflicts', async () => {
    reads(resolution, { ...details, stages: [] })
    show()
    expect(await screen.findByText(/requires both growth stages and resource rules/)).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Verify and activate' })).toBeDisabled()
  })

  it('lets Admin start a replacement with the selected verified profile and a stable key', async () => {
    const user = userEvent.setup()
    reads({ ...resolution, profiles: [{ ...profile, verificationState: 2, isActive: true }],
      compatibleVerifiedProfileIds: ['profile-1'], nextResponsibleRole: 'Admin', canStartReplacement: true },
      { ...details, verificationState: 2, isActive: true })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { workflowId: 'workflow-new' } })
    show(5)
    const start = await screen.findByRole('button', { name: 'Start replacement workflow' })
    await user.click(start)
    expect(post).toHaveBeenCalledWith('/crop-plans/plan-1/replace-blocked-workflow', expect.objectContaining({
      blockedWorkflowId: 'workflow-1', verifiedProfileId: 'profile-1', idempotencyKey: expect.any(String),
    }))
    expect(await screen.findByRole('link', { name: 'Open replacement workflow' })).toHaveAttribute('href', '/task-approval/workflows/workflow-new')
    expect(screen.queryByRole('button', { name: 'Verify and activate' })).not.toBeInTheDocument()
  })

  it('shows the successor and hides verification controls from other roles', async () => {
    reads({ ...resolution, successorWorkflowId: 'successor-1' })
    show(2)
    expect(await screen.findByRole('link', { name: 'Open replacement workflow' })).toHaveAttribute('href', '/task-approval/workflows/successor-1')
    expect(screen.queryByRole('button', { name: 'Verify and activate' })).not.toBeInTheDocument()
  })
})
