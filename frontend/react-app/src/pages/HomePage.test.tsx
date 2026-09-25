import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { HomePage } from './HomePage'

function renderHome() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  )
}

describe('HomePage', () => {
  it('presents the platform with its public destinations', () => {
    renderHome()

    expect(screen.getByRole('heading', { level: 1, name: /smart agricultural planning/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /learn more/i })).toHaveAttribute('href', '/about')
    const loginLinks = screen.getAllByRole('link', { name: /staff login/i })
    expect(loginLinks).toHaveLength(2)
    loginLinks.forEach((link) => expect(link).toHaveAttribute('href', '/login'))
    expect(screen.getByRole('heading', { name: /built around real agricultural workflows/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /clear responsibilities across the farm operation/i })).toBeInTheDocument()
  })

  it('lists the existing platform capabilities', () => {
    renderHome()

    for (const capability of [
      'Crop & Season Planning',
      'Field Inspections & Crop Issues',
      'Resource & Inventory Management',
      'Weather Forecasts',
      'Tasks, Irrigation & Approvals',
      'AI-Assisted Planning Workflow',
    ]) {
      expect(screen.getByRole('heading', { name: capability })).toBeInTheDocument()
    }
  })

  it('keeps the complete request-to-approval workflow and all five roles', () => {
    renderHome()

    const workflow = screen.getByRole('list', { name: /request to approval workflow/i })
    expect(within(workflow).getAllByRole('listitem')).toHaveLength(5)
    for (const phase of ['Plan', 'Analyze', 'Manage Resources', 'Monitor', 'Act']) {
      expect(within(workflow).getByText(phase)).toBeInTheDocument()
    }
    expect(within(workflow).getByText('Officer approval')).toBeInTheDocument()

    for (const role of ['Farmer', 'Field Officer', 'Resource Officer', 'Agricultural Officer', 'Admin']) {
      expect(screen.getByRole('heading', { name: role })).toBeInTheDocument()
    }
  })

  it('does not present statistics or testimonials', () => {
    renderHome()

    expect(screen.queryByText(/testimonial/i)).not.toBeInTheDocument()
    expect(screen.queryByText(/\d+\s*%/)).not.toBeInTheDocument()
  })
})
