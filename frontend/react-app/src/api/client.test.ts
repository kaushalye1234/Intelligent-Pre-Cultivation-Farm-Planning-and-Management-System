import { describe, expect, it } from 'vitest'
import { resolveApiBaseUrl } from './client'

describe('resolveApiBaseUrl', () => {
  it('adds the API path to a Render service URL', () => {
    expect(resolveApiBaseUrl('https://agriassist-api.onrender.com')).toBe(
      'https://agriassist-api.onrender.com/api',
    )
  })

  it('preserves a configured base URL that already ends in /api', () => {
    expect(resolveApiBaseUrl('http://localhost:5087/api/')).toBe('http://localhost:5087/api')
  })

  it('uses the local API when no base URL is configured', () => {
    expect(resolveApiBaseUrl()).toBe('http://localhost:5087/api')
  })
})
