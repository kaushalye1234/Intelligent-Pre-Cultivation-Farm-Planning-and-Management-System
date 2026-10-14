import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { FinalGuideRecoveryPanel } from './FinalGuideRecoveryPanel'

afterEach(() => vi.restoreAllMocks())

it('keeps approved work visible and retries the approved revision', async () => {
  const user = userEvent.setup()
  const refresh = vi.fn().mockResolvedValue(undefined)
  const post = vi.spyOn(api, 'post').mockResolvedValue({ data: { status: 'Ready', approvedRevision: 2 } })
  render(<FinalGuideRecoveryPanel workflowId="workflow-1" revision={2} status="Unavailable" canRetry onRefresh={refresh} />)
  expect(screen.getByText(/Approved work remains available/)).toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Retry farmer guide' }))
  await waitFor(() => expect(post).toHaveBeenCalledWith('/task-approval/workflows/workflow-1/generate-final-guide', null, {
    params: { approvedRevision: 2 },
  }))
  expect(await screen.findByText(/Guide status: Ready/)).toBeInTheDocument()
  expect(refresh).toHaveBeenCalledOnce()
})

it('reports provider failure and leaves retry available', async () => {
  const user = userEvent.setup()
  vi.spyOn(api, 'post').mockRejectedValue(new Error('Provider unavailable'))
  render(<FinalGuideRecoveryPanel workflowId="workflow-1" revision={2} status="Unavailable" canRetry onRefresh={vi.fn()} />)
  await user.click(screen.getByRole('button', { name: 'Retry farmer guide' }))
  expect(await screen.findByText(/Provider unavailable/)).toBeInTheDocument()
  expect(screen.getByRole('button', { name: 'Retry farmer guide' })).toBeEnabled()
})

it('shows generation date and omits retry for ready guides or unauthorized staff', () => {
  render(<FinalGuideRecoveryPanel workflowId="workflow-1" revision={2} status="Ready" generatedAt="2026-10-07T10:00:00Z" canRetry={false} onRefresh={vi.fn()} />)
  expect(screen.getByText(/Generated/)).toBeInTheDocument()
  expect(screen.queryByRole('button', { name: 'Retry farmer guide' })).not.toBeInTheDocument()
})
