import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { HomePage } from './HomePage'

function setReducedMotion(matches: boolean) {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: query === '(prefers-reduced-motion: reduce)' ? matches : false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  })
}

function renderHome() {
  return render(
    <MemoryRouter>
      <HomePage />
    </MemoryRouter>,
  )
}

describe('HomePage', () => {
  beforeEach(() => {
    setReducedMotion(false)
  })

  it('preserves the public homepage content and destinations', () => {
    renderHome()

    expect(screen.getByRole('heading', { name: /smart agricultural planning/i })).toBeInTheDocument()
    expect(screen.getByRole('link', { name: /learn more/i })).toHaveAttribute('href', '/about')
    expect(screen.getAllByRole('link', { name: /staff login/i })).toHaveLength(2)
    expect(screen.getByRole('heading', { name: /built around real agricultural workflows/i })).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: /clear responsibilities across the farm operation/i })).toBeInTheDocument()
  })

  it('keeps the complete request-to-approval workflow and all five roles', () => {
    renderHome()

    const workflow = screen.getByRole('list')
    expect(within(workflow).getAllByRole('listitem')).toHaveLength(5)
    expect(within(workflow).getByText('Farmer submits crop plan request')).toBeInTheDocument()
    expect(within(workflow).getByText('Agricultural officer approval')).toBeInTheDocument()

    for (const role of ['Farmer', 'Field Officer', 'Resource Officer', 'Agricultural Officer', 'Admin']) {
      expect(screen.getByRole('heading', { name: role })).toBeInTheDocument()
    }
  })

  it('renders the mature plant without reveal setup when reduced motion is requested', () => {
    setReducedMotion(true)
    const { container } = renderHome()
    const page = container.querySelector('.home-page')
    const plantRail = container.querySelector('.home-growth-rail')

    expect(page).not.toHaveClass('home-reveal-ready')
    expect(plantRail).toHaveStyle({ '--root-growth': '1', '--stem-growth': '1', '--crown-growth': '1' })
    expect(plantRail).toHaveAttribute('aria-hidden', 'true')
  })
})
