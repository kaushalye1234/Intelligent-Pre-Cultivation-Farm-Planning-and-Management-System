import type { WeatherRiskLevel } from './types'

// Shared by the Resource Officer queue, the analysis history and the stored AI result view.
export const weatherResourceHistoryUrl = '/crop-plans/weather-resource-history'

export type Tone = 'neutral' | 'good' | 'warn' | 'bad' | 'info'

export function humanize(value?: string | null) {
  if (!value) return ''
  const words = value.replace(/([a-z])([A-Z])/g, '$1 $2').toLowerCase()
  return words.charAt(0).toUpperCase() + words.slice(1)
}

export function riskTone(level?: WeatherRiskLevel | string | null): Tone {
  if (level === 'Low') return 'good'
  if (level === 'Medium') return 'warn'
  if (level === 'High') return 'bad'
  return 'neutral'
}

export function requirementTone(status?: string | null): Tone {
  if (status === 'Sufficient') return 'good'
  if (status === 'Insufficient') return 'bad'
  return 'warn'
}
