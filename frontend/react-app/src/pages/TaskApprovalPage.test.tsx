import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { api } from '../api/client'
import { AuthContext } from '../auth/AuthContext'
import type { UserProfile } from '../types'
import { TaskApprovalPage } from './TaskApprovalPage'

const adminUser: UserProfile = {
  id: 'admin-user-id',
  fullName: 'Development Admin',
  email: 'admin@agriassist.local',
  role: 5,
  isActive: true,
  mustChangePassword: false,
}

afterEach(() => {
  vi.restoreAllMocks()
})

describe('TaskApprovalPage', () => {
  it('submits the officer-written rejection reason', async () => {
    const task = {
      id: 'task-1',
      farmId: 'farm-1',
      title: 'Inspect field',
      description: 'Check crop condition',
      dueAt: new Date(Date.now() + 86_400_000).toISOString(),
      assignedToUserId: adminUser.id,
      status: 2,
    }
    vi.spyOn(api, 'get').mockImplementation(async (url) => {
      const items = url.includes('/crop-planning/farms')
        ? [{ id: 'farm-1', name: 'North Farm', location: 'North', totalArea: 10, ownerUserId: 'farmer-1' }]
        : url.includes('/task-approval/tasks')
          ? [task]
          : url.includes('/users')
            ? [adminUser]
            : []
      return { data: { items, page: 1, pageSize: 20, totalCount: items.length, totalPages: 1 } } as never
    })
    const post = vi.spyOn(api, 'post').mockResolvedValue({ data: {} } as never)

    render(
      <MemoryRouter>
        <AuthContext.Provider value={{
          user: adminUser,
          token: 'token',
          isAuthenticated: true,
          isLoading: false,
          passwordChangeUser: null,
          hasPasswordChangeSession: false,
          login: vi.fn(),
          changeTemporaryPassword: vi.fn(),
          logout: vi.fn(),
        }}>
          <TaskApprovalPage />
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    await userEvent.click(await screen.findByRole('tab', { name: /farm tasks/i }))
    await screen.findByText('Inspect field')
    await userEvent.click(screen.getByRole('button', { name: /^reject$/i }))
    await userEvent.type(screen.getByLabelText(/^reason/i), 'Weather risk is too high')
    await userEvent.click(screen.getByRole('button', { name: /reject task/i }))

    await waitFor(() => expect(post).toHaveBeenCalledWith('/task-approval/tasks/task-1/reject', {
      comment: 'Weather risk is too high',
      agentWorkflowId: null,
    }))
  })
})
