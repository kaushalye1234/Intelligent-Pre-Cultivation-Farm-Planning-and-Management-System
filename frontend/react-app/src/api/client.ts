import axios, { AxiosError } from 'axios'

type ApiErrorPayload = {
  message?: string
  title?: string
  error?: {
    code?: string
    message?: string
  }
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5087/api'

export const api = axios.create({
  baseURL: apiBaseUrl,
  headers: {
    'Content-Type': 'application/json',
  },
})

export function setAuthToken(token: string | null) {
  if (token) {
    api.defaults.headers.common.Authorization = `Bearer ${token}`
    return
  }

  delete api.defaults.headers.common.Authorization
}

export function getErrorMessage(error: unknown): string {
  if (axios.isAxiosError(error)) {
    const axiosError = error as AxiosError<ApiErrorPayload>
    return axiosError.response?.data?.error?.message ?? axiosError.response?.data?.message ?? axiosError.response?.data?.title ?? axiosError.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error'
}

export function getErrorCode(error: unknown): string | undefined {
  if (!axios.isAxiosError(error)) return undefined
  return (error as AxiosError<ApiErrorPayload>).response?.data?.error?.code
}
