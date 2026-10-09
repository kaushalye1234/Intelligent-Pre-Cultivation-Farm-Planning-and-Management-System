import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { resolve } from 'node:path'
import { parseSchedulingOutput, safeSourceUrl } from './schedulingProposal'

function golden(name: string): unknown {
  const path = resolve(process.cwd(), '../../docs/ai-usage/fixtures/member2', name)
  return JSON.parse(readFileSync(path, 'utf8'))
}

describe('scheduling proposal display parser', () => {
  it('reads a version-2 blocked proposal with per-item evidence', () => {
    const proposal = parseSchedulingOutput({
      contractVersion: 2, status: 'CandidateBlocked', warnings: ['Weather risk is High'],
      constraints: [{ code: 'HIGH_WEATHER_RISK', severity: 'Blocking', message: 'High weather blocks approval.' }],
      candidateTasks: [{ title: 'Review Planting stage', dueAt: '2026-10-10T08:00:00Z',
        reason: 'Verified stage 1', sources: [{ kind: 'CropStage', id: 'stage-1', label: 'Guide', sourceUrl: 'https://example.test/guide' }] }],
      candidateIrrigation: [], candidateReservations: [],
    })
    expect(proposal?.tasks[0].sources[0].label).toBe('Guide')
    expect(proposal?.blocking).toContain('High weather blocks approval.')
  })

  it('does not promote legacy or malformed output into the concise proposal', () => {
    expect(parseSchedulingOutput({ status: 'CandidateReady', candidateTasks: [] })).toBeNull()
    expect(parseSchedulingOutput({ contractVersion: 2, status: 'CandidateReady',
      candidateTasks: [{ title: 'Unsupported' }], candidateIrrigation: [], candidateReservations: [] })).toBeNull()
  })

  it('allows only HTTP source links', () => {
    expect(safeSourceUrl('https://example.test/guide')).toBe('https://example.test/guide')
    expect(safeSourceUrl('javascript:alert(1)')).toBeNull()
  })

  it('parses the shared pending crop-health proposal fixture without semantic rewriting', () => {
    const proposal = parseSchedulingOutput(golden('member4-proposal.pending.valid.json'))

    expect(proposal?.cropHealthGuidance?.decision).toBe('PendingDecision')
    expect(proposal?.cropHealthTasks[0]).toMatchObject({
      actionType: 'RemoveAffectedResidue',
      title: 'Remove affected crop residues',
      included: true,
    })
  })
})
