$ErrorActionPreference = "Stop"

$appRoot = Join-Path $PSScriptRoot "..\frontend\react-app"
$srcRoot = Join-Path $appRoot "src"

New-Item -ItemType Directory -Force -Path (Join-Path $srcRoot "api") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $srcRoot "auth") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $srcRoot "components") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $srcRoot "pages") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $srcRoot "test") | Out-Null

$packagePath = Join-Path $appRoot "package.json"
$packageJson = Get-Content $packagePath -Raw | ConvertFrom-Json
$packageJson.name = "agriassist-react-app"
$packageJson.scripts | Add-Member -NotePropertyName "test" -NotePropertyValue "vitest --run" -Force
$packageJson.scripts | Add-Member -NotePropertyName "test:watch" -NotePropertyValue "vitest" -Force
$packageJson.scripts.lint = "oxlint ."
$packageJson | ConvertTo-Json -Depth 20 | Set-Content -Path $packagePath -Encoding UTF8

@'
VITE_API_BASE_URL=http://localhost:5000/api
'@ | Set-Content -Path (Join-Path $appRoot ".env.example") -Encoding UTF8

@'
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
  },
})
'@ | Set-Content -Path (Join-Path $appRoot "vite.config.ts") -Encoding UTF8

@'
import '@testing-library/jest-dom/vitest'
'@ | Set-Content -Path (Join-Path $srcRoot "test\setup.ts") -Encoding UTF8

@'
export type ApplicationRole = 1 | 2 | 3 | 4 | 5

export type UserProfile = {
  id: string
  fullName: string
  email: string
  role: ApplicationRole
  isActive: boolean
}

export type AuthResponse = {
  accessToken: string
  expiresAt: string
  user: UserProfile
}

export type PagedResult<T> = {
  items: T[]
  page: number
  pageSize: number
  totalCount: number
  totalPages: number
}

export type DashboardSummary = {
  usersByRole: { role: string; count: number }[]
  activeFarms: number
  activeCropPlans: number
  openCropIssues: number
  lowStockResources: number
  pendingTasks: number
  pendingApprovals: number
}

export type Farm = {
  id: string
  name: string
  location: string
  totalArea: number
  ownerUserId: string
  createdAt: string
}

export type Field = {
  id: string
  farmId: string
  name: string
  area: number
  soilType: string
  isActive: boolean
}

export type CropType = {
  id: string
  name: string
  description?: string
  isActive: boolean
}

export type CropPlan = {
  id: string
  farmId: string
  fieldId?: string
  cropTypeId: string
  preferredStartDate: string
  preferredEndDate: string
  budget: number
  objective: string
  status: number
  createdAt: string
}

export type Inspection = {
  id: string
  fieldId: string
  inspectorUserId: string
  scheduledAt: string
  completedAt?: string
  status: number
  summary: string
}

export type Observation = {
  id: string
  fieldInspectionId: string
  observationType: string
  notes: string
}

export type CropIssue = {
  id: string
  fieldInspectionId: string
  title: string
  description: string
  severity: number
  status: number
  escalatedAt?: string
}

export type ResourceCategory = {
  id: string
  name: string
  description?: string
}

export type Supplier = {
  id: string
  name: string
  contactEmail: string
  phone: string
}

export type ResourceItem = {
  id: string
  resourceCategoryId: string
  supplierId?: string
  name: string
  unit: string
  isActive: boolean
}

export type InventoryStock = {
  id: string
  resourceId: string
  quantityOnHand: number
  reservedQuantity: number
  availableQuantity: number
  lowStockThreshold: number
}

export type Reservation = {
  id: string
  inventoryStockId: string
  requestedByUserId: string
  quantity: number
  status: number
  releasedAt?: string
  purpose: string
}

export type FarmTask = {
  id: string
  farmId: string
  title: string
  description: string
  dueAt: string
  assignedToUserId: string
  status: number
}

export type IrrigationSchedule = {
  id: string
  fieldId: string
  scheduledAt: string
  durationMinutes: number
  notes: string
  status: number
}

export type ApprovalDecision = {
  id: string
  farmTaskId?: string
  irrigationScheduleId?: string
  decidedByUserId: string
  agentWorkflowId?: string
  decision: number
  comment: string
  createdAt: string
}
'@ | Set-Content -Path (Join-Path $srcRoot "types.ts") -Encoding UTF8

@'
import axios, { AxiosError } from 'axios'

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5000/api'

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
    const axiosError = error as AxiosError<{ message?: string; title?: string }>
    return axiosError.response?.data?.message ?? axiosError.response?.data?.title ?? axiosError.message
  }

  if (error instanceof Error) {
    return error.message
  }

  return 'Unexpected error'
}
'@ | Set-Content -Path (Join-Path $srcRoot "api\client.ts") -Encoding UTF8

@'
import { createContext, useContext, useEffect, useMemo, useState } from 'react'
import { api, setAuthToken } from '../api/client'
import type { AuthResponse, UserProfile } from '../types'

type AuthContextValue = {
  user: UserProfile | null
  token: string | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (email: string, password: string) => Promise<void>
  logout: () => void
}

const storageKey = 'agriassist.auth'

export const AuthContext = createContext<AuthContextValue | undefined>(undefined)

type StoredSession = {
  token: string
  user: UserProfile
}

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [session, setSession] = useState<StoredSession | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    const stored = window.localStorage.getItem(storageKey)
    if (stored) {
      const parsed = JSON.parse(stored) as StoredSession
      setSession(parsed)
      setAuthToken(parsed.token)
    }
    setIsLoading(false)
  }, [])

  async function login(email: string, password: string) {
    const response = await api.post<AuthResponse>('/auth/login', { email, password })
    const nextSession = {
      token: response.data.accessToken,
      user: response.data.user,
    }
    window.localStorage.setItem(storageKey, JSON.stringify(nextSession))
    setAuthToken(nextSession.token)
    setSession(nextSession)
  }

  function logout() {
    window.localStorage.removeItem(storageKey)
    setAuthToken(null)
    setSession(null)
  }

  const value = useMemo<AuthContextValue>(
    () => ({
      user: session?.user ?? null,
      token: session?.token ?? null,
      isAuthenticated: Boolean(session?.token),
      isLoading,
      login,
      logout,
    }),
    [isLoading, session],
  )

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used inside AuthProvider')
  }

  return context
}
'@ | Set-Content -Path (Join-Path $srcRoot "auth\AuthContext.tsx") -Encoding UTF8

@'
import type { ApplicationRole } from './types'

export const roleLabels: Record<ApplicationRole, string> = {
  1: 'Farmer',
  2: 'Field Officer',
  3: 'Resource Officer',
  4: 'Agricultural Officer',
  5: 'Admin',
}

export const cropPlanStatus: Record<number, string> = {
  1: 'Draft',
  2: 'Submitted',
  3: 'Preliminary',
  4: 'Approved',
  5: 'Rejected',
  6: 'Cancelled',
}

export const inspectionStatus: Record<number, string> = {
  1: 'Scheduled',
  2: 'In progress',
  3: 'Completed',
  4: 'Escalated',
  5: 'Cancelled',
}

export const issueSeverity: Record<number, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
}

export const issueStatus: Record<number, string> = {
  1: 'Open',
  2: 'Escalated',
  3: 'Resolved',
  4: 'Closed',
}

export const taskStatus: Record<number, string> = {
  1: 'Draft',
  2: 'Pending',
  3: 'Approved',
  4: 'Rejected',
  5: 'Revision',
  6: 'Completed',
  7: 'Cancelled',
}

export const scheduleStatus: Record<number, string> = {
  1: 'Pending',
  2: 'Approved',
  3: 'Rejected',
  4: 'Revision',
  5: 'Completed',
  6: 'Cancelled',
}

export const reservationStatus: Record<number, string> = {
  1: 'Reserved',
  2: 'Released',
  3: 'Cancelled',
}
'@ | Set-Content -Path (Join-Path $srcRoot "labels.ts") -Encoding UTF8

@'
import { AlertTriangle, CheckCircle2, Loader2 } from 'lucide-react'

export function LoadingState({ label = 'Loading data' }: { label?: string }) {
  return (
    <div className="state-box" role="status">
      <Loader2 className="spin" size={20} aria-hidden="true" />
      <span>{label}</span>
    </div>
  )
}

export function ErrorState({ message }: { message: string }) {
  return (
    <div className="state-box state-box-error" role="alert">
      <AlertTriangle size={20} aria-hidden="true" />
      <span>{message}</span>
    </div>
  )
}

export function EmptyState({ message }: { message: string }) {
  return (
    <div className="state-box">
      <CheckCircle2 size={20} aria-hidden="true" />
      <span>{message}</span>
    </div>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\States.tsx") -Encoding UTF8

@'
import { EmptyState } from './States'

type Column<T> = {
  header: string
  render: (row: T) => React.ReactNode
}

export function DataTable<T>({
  columns,
  rows,
  emptyMessage,
}: {
  columns: Column<T>[]
  rows: T[]
  emptyMessage: string
}) {
  if (rows.length === 0) {
    return <EmptyState message={emptyMessage} />
  }

  return (
    <div className="table-wrap">
      <table>
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column.header}>{column.header}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, index) => (
            <tr key={index}>
              {columns.map((column) => (
                <td key={column.header}>{column.render(row)}</td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\DataTable.tsx") -Encoding UTF8

@'
export function StatusPill({ label, tone = 'neutral' }: { label: string; tone?: 'neutral' | 'good' | 'warn' | 'bad' }) {
  return <span className={`status-pill status-${tone}`}>{label}</span>
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\StatusPill.tsx") -Encoding UTF8

@'
import type { FormEvent } from 'react'

export type SelectOption = {
  value: string | number
  label: string
}

export function TextInput({
  label,
  value,
  onChange,
  type = 'text',
  required,
  placeholder,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  type?: string
  required?: boolean
  placeholder?: string
}) {
  return (
    <label className="field-control">
      <span>{label}</span>
      <input required={required} type={type} value={value} placeholder={placeholder} onChange={(event) => onChange(event.target.value)} />
    </label>
  )
}

export function SelectInput({
  label,
  value,
  options,
  onChange,
  required,
}: {
  label: string
  value: string | number
  options: SelectOption[]
  onChange: (value: string) => void
  required?: boolean
}) {
  return (
    <label className="field-control">
      <span>{label}</span>
      <select required={required} value={value} onChange={(event) => onChange(event.target.value)}>
        <option value="">Select</option>
        {options.map((option) => (
          <option key={option.value} value={option.value}>
            {option.label}
          </option>
        ))}
      </select>
    </label>
  )
}

export function FormPanel({
  title,
  onSubmit,
  children,
  submitLabel,
}: {
  title: string
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
  children: React.ReactNode
  submitLabel: string
}) {
  return (
    <form className="form-panel" onSubmit={onSubmit}>
      <h3>{title}</h3>
      <div className="form-grid">{children}</div>
      <button type="submit" className="primary-button">
        {submitLabel}
      </button>
    </form>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\FormControls.tsx") -Encoding UTF8

@'
import { NavLink, Outlet, useNavigate } from 'react-router-dom'
import { BarChart3, ClipboardCheck, Leaf, LogOut, Package, ShieldCheck, Sprout, Users } from 'lucide-react'
import { useAuth } from '../auth/AuthContext'
import { roleLabels } from '../labels'

const navItems = [
  { to: '/', label: 'Dashboard', icon: BarChart3 },
  { to: '/crop-planning', label: 'Crop planning', icon: Sprout },
  { to: '/inspections', label: 'Inspections', icon: ClipboardCheck },
  { to: '/resources', label: 'Resources', icon: Package },
  { to: '/task-approval', label: 'Task approval', icon: ShieldCheck },
  { to: '/users', label: 'Users', icon: Users },
]

export function Layout() {
  const { user, logout } = useAuth()
  const navigate = useNavigate()

  function handleLogout() {
    logout()
    navigate('/login')
  }

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          <Leaf size={26} aria-hidden="true" />
          <div>
            <strong>AgriAssist</strong>
            <span>Operations Console</span>
          </div>
        </div>
        <nav aria-label="Main navigation">
          {navItems.map((item) => {
            const Icon = item.icon
            return (
              <NavLink key={item.to} to={item.to} end={item.to === '/'}>
                <Icon size={18} aria-hidden="true" />
                <span>{item.label}</span>
              </NavLink>
            )
          })}
        </nav>
        <div className="sidebar-footer">
          <div>
            <strong>{user?.fullName}</strong>
            <span>{user ? roleLabels[user.role] : ''}</span>
          </div>
          <button type="button" className="icon-button" onClick={handleLogout} aria-label="Log out" title="Log out">
            <LogOut size={18} aria-hidden="true" />
          </button>
        </div>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\Layout.tsx") -Encoding UTF8

@'
import { Navigate, Outlet } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { LoadingState } from './States'

export function ProtectedRoute() {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return <LoadingState label="Checking session" />
  }

  if (!isAuthenticated) {
    return <Navigate to="/login" replace />
  }

  return <Outlet />
}
'@ | Set-Content -Path (Join-Path $srcRoot "components\ProtectedRoute.tsx") -Encoding UTF8

@'
import { FormEvent, useState } from 'react'
import { Navigate, useNavigate } from 'react-router-dom'
import { Leaf } from 'lucide-react'
import { getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'

export function LoginPage() {
  const { isAuthenticated, login } = useAuth()
  const navigate = useNavigate()
  const [email, setEmail] = useState('admin@agriassist.local')
  const [password, setPassword] = useState('')
  const [error, setError] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)

  if (isAuthenticated) {
    return <Navigate to="/" replace />
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setError('')

    if (!email.trim() || !password.trim()) {
      setError('Email and password are required.')
      return
    }

    setIsSubmitting(true)
    try {
      await login(email.trim(), password)
      navigate('/')
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="login-screen">
      <section className="login-panel">
        <div className="login-mark">
          <Leaf size={34} aria-hidden="true" />
          <div>
            <h1>AgriAssist</h1>
            <p>Staff and admin operations console</p>
          </div>
        </div>
        <form onSubmit={handleSubmit} className="login-form" noValidate>
          <label>
            <span>Email</span>
            <input value={email} type="email" onChange={(event) => setEmail(event.target.value)} />
          </label>
          <label>
            <span>Password</span>
            <input value={password} type="password" onChange={(event) => setPassword(event.target.value)} />
          </label>
          {error ? <div role="alert" className="form-error">{error}</div> : null}
          <button type="submit" className="primary-button" disabled={isSubmitting}>
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\LoginPage.tsx") -Encoding UTF8

@'
import { useEffect, useState } from 'react'
import { Activity, AlertTriangle, ClipboardList, PackageCheck, ShieldCheck, Sprout, Users } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { roleLabels } from '../labels'
import type { DashboardSummary } from '../types'
import { ErrorState, LoadingState } from '../components/States'

const metricIcons = [Sprout, Activity, AlertTriangle, PackageCheck, ClipboardList, ShieldCheck]

export function DashboardPage() {
  const [summary, setSummary] = useState<DashboardSummary | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState('')

  useEffect(() => {
    api
      .get<DashboardSummary>('/dashboard/summary')
      .then((response) => setSummary(response.data))
      .catch((err) => setError(getErrorMessage(err)))
      .finally(() => setIsLoading(false))
  }, [])

  if (isLoading) return <LoadingState />
  if (error) return <ErrorState message={error} />
  if (!summary) return null

  const metrics = [
    ['Active farms', summary.activeFarms],
    ['Active crop plans', summary.activeCropPlans],
    ['Open crop issues', summary.openCropIssues],
    ['Low stock resources', summary.lowStockResources],
    ['Pending tasks', summary.pendingTasks],
    ['Pending approvals', summary.pendingApprovals],
  ] as const

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>Operational snapshot</p>
          <h2>Dashboard</h2>
        </div>
      </header>
      <div className="metric-grid">
        {metrics.map(([label, value], index) => {
          const Icon = metricIcons[index]
          return (
            <article className="metric-card" key={label}>
              <Icon size={22} aria-hidden="true" />
              <span>{label}</span>
              <strong>{value}</strong>
            </article>
          )
        })}
      </div>
      <section className="work-section">
        <div className="section-title">
          <Users size={18} aria-hidden="true" />
          <h3>Users by role</h3>
        </div>
        <div className="role-strip">
          {summary.usersByRole.map((roleCount) => (
            <span key={roleCount.role}>
              {roleLabels[Number(roleCount.role) as keyof typeof roleLabels] ?? roleCount.role}: <strong>{roleCount.count}</strong>
            </span>
          ))}
        </div>
      </section>
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\DashboardPage.tsx") -Encoding UTF8

@'
import { FormEvent, useEffect, useMemo, useState } from 'react'
import { Plus, Search } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { FormPanel, SelectInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { cropPlanStatus } from '../labels'
import type { CropPlan, CropType, Farm, Field, PagedResult } from '../types'

export function CropPlanningPage() {
  const [farms, setFarms] = useState<Farm[]>([])
  const [fields, setFields] = useState<Field[]>([])
  const [cropTypes, setCropTypes] = useState<CropType[]>([])
  const [requests, setRequests] = useState<CropPlan[]>([])
  const [search, setSearch] = useState('')
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [farmForm, setFarmForm] = useState({ name: '', location: '', totalArea: '' })
  const [fieldForm, setFieldForm] = useState({ farmId: '', name: '', area: '', soilType: '' })
  const [planForm, setPlanForm] = useState({ farmId: '', fieldId: '', cropTypeId: '', preferredStartDate: '', preferredEndDate: '', budget: '', objective: '' })

  const farmOptions = farms.map((farm) => ({ value: farm.id, label: farm.name }))
  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const cropTypeOptions = cropTypes.map((cropType) => ({ value: cropType.id, label: cropType.name }))

  async function loadData(nextSearch = search) {
    setIsLoading(true)
    setError('')
    try {
      const [farmResult, fieldResult, cropTypeResult, requestResult] = await Promise.all([
        api.get<PagedResult<Farm>>('/crop-planning/farms', { params: { search: nextSearch, sortBy: 'name' } }),
        api.get<PagedResult<Field>>('/crop-planning/fields', { params: { sortBy: 'name' } }),
        api.get<PagedResult<CropType>>('/crop-planning/crop-types', { params: { sortBy: 'name' } }),
        api.get<PagedResult<CropPlan>>('/crop-planning/requests', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFarms(farmResult.data.items)
      setFields(fieldResult.data.items)
      setCropTypes(cropTypeResult.data.items)
      setRequests(requestResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData('')
  }, [])

  const farmNameById = useMemo(() => new Map(farms.map((farm) => [farm.id, farm.name])), [farms])
  const cropNameById = useMemo(() => new Map(cropTypes.map((crop) => [crop.id, crop.name])), [cropTypes])

  async function createFarm(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/crop-planning/farms', { ...farmForm, totalArea: Number(farmForm.totalArea), ownerUserId: null })
    setFarmForm({ name: '', location: '', totalArea: '' })
    await loadData()
  }

  async function createField(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/crop-planning/fields', { ...fieldForm, area: Number(fieldForm.area), isActive: true })
    setFieldForm({ farmId: '', name: '', area: '', soilType: '' })
    await loadData()
  }

  async function createPreliminary(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/crop-planning/requests/preliminary', {
      farmId: planForm.farmId,
      fieldId: planForm.fieldId || null,
      cropTypeId: planForm.cropTypeId,
      preferredStartDate: planForm.preferredStartDate,
      preferredEndDate: planForm.preferredEndDate,
      budget: Number(planForm.budget),
      objective: planForm.objective,
    })
    setPlanForm({ farmId: '', fieldId: '', cropTypeId: '', preferredStartDate: '', preferredEndDate: '', budget: '', objective: '' })
    await loadData()
  }

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>CropPlanning</p>
          <h2>Farms, fields, crop types, and plan requests</h2>
        </div>
        <form className="search-box" onSubmit={(event) => { event.preventDefault(); void loadData(search) }}>
          <Search size={16} aria-hidden="true" />
          <input value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Search farms" />
          <button type="submit">Search</button>
        </form>
      </header>
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <>
          <div className="work-grid">
            <FormPanel title="Add farm" onSubmit={createFarm} submitLabel="Create farm">
              <TextInput label="Name" value={farmForm.name} required onChange={(value) => setFarmForm({ ...farmForm, name: value })} />
              <TextInput label="Location" value={farmForm.location} required onChange={(value) => setFarmForm({ ...farmForm, location: value })} />
              <TextInput label="Total area" value={farmForm.totalArea} type="number" required onChange={(value) => setFarmForm({ ...farmForm, totalArea: value })} />
            </FormPanel>
            <FormPanel title="Add field" onSubmit={createField} submitLabel="Create field">
              <SelectInput label="Farm" value={fieldForm.farmId} required options={farmOptions} onChange={(value) => setFieldForm({ ...fieldForm, farmId: value })} />
              <TextInput label="Name" value={fieldForm.name} required onChange={(value) => setFieldForm({ ...fieldForm, name: value })} />
              <TextInput label="Area" value={fieldForm.area} type="number" required onChange={(value) => setFieldForm({ ...fieldForm, area: value })} />
              <TextInput label="Soil type" value={fieldForm.soilType} required onChange={(value) => setFieldForm({ ...fieldForm, soilType: value })} />
            </FormPanel>
            <FormPanel title="Generate preliminary request" onSubmit={createPreliminary} submitLabel="Generate">
              <SelectInput label="Farm" value={planForm.farmId} required options={farmOptions} onChange={(value) => setPlanForm({ ...planForm, farmId: value })} />
              <SelectInput label="Field" value={planForm.fieldId} options={fieldOptions} onChange={(value) => setPlanForm({ ...planForm, fieldId: value })} />
              <SelectInput label="Crop type" value={planForm.cropTypeId} required options={cropTypeOptions} onChange={(value) => setPlanForm({ ...planForm, cropTypeId: value })} />
              <TextInput label="Start date" type="date" value={planForm.preferredStartDate} required onChange={(value) => setPlanForm({ ...planForm, preferredStartDate: value })} />
              <TextInput label="End date" type="date" value={planForm.preferredEndDate} required onChange={(value) => setPlanForm({ ...planForm, preferredEndDate: value })} />
              <TextInput label="Budget" type="number" value={planForm.budget} required onChange={(value) => setPlanForm({ ...planForm, budget: value })} />
              <TextInput label="Objective" value={planForm.objective} required onChange={(value) => setPlanForm({ ...planForm, objective: value })} />
            </FormPanel>
          </div>
          <section className="work-section">
            <div className="section-title"><Plus size={18} aria-hidden="true" /><h3>Crop plan requests</h3></div>
            <DataTable
              rows={requests}
              emptyMessage="No crop plan requests found."
              columns={[
                { header: 'Farm', render: (row) => farmNameById.get(row.farmId) ?? row.farmId.slice(0, 8) },
                { header: 'Crop', render: (row) => cropNameById.get(row.cropTypeId) ?? row.cropTypeId.slice(0, 8) },
                { header: 'Window', render: (row) => `${row.preferredStartDate} to ${row.preferredEndDate}` },
                { header: 'Budget', render: (row) => row.budget.toLocaleString() },
                { header: 'Status', render: (row) => <StatusPill label={cropPlanStatus[row.status] ?? String(row.status)} tone={row.status === 4 ? 'good' : row.status === 5 ? 'bad' : 'warn'} /> },
              ]}
            />
          </section>
        </>
      )}
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\CropPlanningPage.tsx") -Encoding UTF8

@'
import { FormEvent, useEffect, useState } from 'react'
import { AlertTriangle, Camera, ClipboardCheck } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { FormPanel, SelectInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { inspectionStatus, issueSeverity, issueStatus } from '../labels'
import type { CropIssue, Field, Inspection, Observation, PagedResult } from '../types'

export function InspectionsPage() {
  const [fields, setFields] = useState<Field[]>([])
  const [inspections, setInspections] = useState<Inspection[]>([])
  const [observations, setObservations] = useState<Observation[]>([])
  const [issues, setIssues] = useState<CropIssue[]>([])
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [inspectionForm, setInspectionForm] = useState({ fieldId: '', scheduledAt: '', status: 1, summary: '' })
  const [observationForm, setObservationForm] = useState({ fieldInspectionId: '', observationType: '', notes: '' })
  const [issueForm, setIssueForm] = useState({ fieldInspectionId: '', title: '', description: '', severity: 1, status: 1 })

  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const inspectionOptions = inspections.map((inspection) => ({ value: inspection.id, label: `${inspection.scheduledAt.slice(0, 10)} - ${inspection.summary || inspection.id.slice(0, 8)}` }))

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [fieldResult, inspectionResult, observationResult, issueResult] = await Promise.all([
        api.get<PagedResult<Field>>('/crop-planning/fields', { params: { sortBy: 'name' } }),
        api.get<PagedResult<Inspection>>('/inspections', { params: { sortBy: 'scheduledAt', sortDirection: 'desc' } }),
        api.get<PagedResult<Observation>>('/inspections/observations'),
        api.get<PagedResult<CropIssue>>('/inspections/issues', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFields(fieldResult.data.items)
      setInspections(inspectionResult.data.items)
      setObservations(observationResult.data.items)
      setIssues(issueResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  async function createInspection(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/inspections', { ...inspectionForm, scheduledAt: new Date(inspectionForm.scheduledAt).toISOString() })
    setInspectionForm({ fieldId: '', scheduledAt: '', status: 1, summary: '' })
    await loadData()
  }

  async function createObservation(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/inspections/observations', observationForm)
    setObservationForm({ fieldInspectionId: '', observationType: '', notes: '' })
    await loadData()
  }

  async function createIssue(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/inspections/issues', issueForm)
    setIssueForm({ fieldInspectionId: '', title: '', description: '', severity: 1, status: 1 })
    await loadData()
  }

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>Inspections</p>
          <h2>Field visits, observations, issues, and uploads</h2>
        </div>
      </header>
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <>
          <div className="work-grid">
            <FormPanel title="Schedule inspection" onSubmit={createInspection} submitLabel="Create inspection">
              <SelectInput label="Field" value={inspectionForm.fieldId} required options={fieldOptions} onChange={(value) => setInspectionForm({ ...inspectionForm, fieldId: value })} />
              <TextInput label="Scheduled at" type="datetime-local" value={inspectionForm.scheduledAt} required onChange={(value) => setInspectionForm({ ...inspectionForm, scheduledAt: value })} />
              <SelectInput label="Status" value={inspectionForm.status} options={Object.entries(inspectionStatus).map(([value, label]) => ({ value, label }))} onChange={(value) => setInspectionForm({ ...inspectionForm, status: Number(value) })} />
              <TextInput label="Summary" value={inspectionForm.summary} required onChange={(value) => setInspectionForm({ ...inspectionForm, summary: value })} />
            </FormPanel>
            <FormPanel title="Add observation" onSubmit={createObservation} submitLabel="Create observation">
              <SelectInput label="Inspection" value={observationForm.fieldInspectionId} required options={inspectionOptions} onChange={(value) => setObservationForm({ ...observationForm, fieldInspectionId: value })} />
              <TextInput label="Type" value={observationForm.observationType} required onChange={(value) => setObservationForm({ ...observationForm, observationType: value })} />
              <TextInput label="Notes" value={observationForm.notes} required onChange={(value) => setObservationForm({ ...observationForm, notes: value })} />
            </FormPanel>
            <FormPanel title="Report crop issue" onSubmit={createIssue} submitLabel="Create issue">
              <SelectInput label="Inspection" value={issueForm.fieldInspectionId} required options={inspectionOptions} onChange={(value) => setIssueForm({ ...issueForm, fieldInspectionId: value })} />
              <TextInput label="Title" value={issueForm.title} required onChange={(value) => setIssueForm({ ...issueForm, title: value })} />
              <TextInput label="Description" value={issueForm.description} required onChange={(value) => setIssueForm({ ...issueForm, description: value })} />
              <SelectInput label="Severity" value={issueForm.severity} options={Object.entries(issueSeverity).map(([value, label]) => ({ value, label }))} onChange={(value) => setIssueForm({ ...issueForm, severity: Number(value) })} />
            </FormPanel>
          </div>
          <section className="work-section">
            <div className="section-title"><ClipboardCheck size={18} aria-hidden="true" /><h3>Inspections</h3></div>
            <DataTable rows={inspections} emptyMessage="No inspections found." columns={[
              { header: 'Date', render: (row) => row.scheduledAt.slice(0, 16).replace('T', ' ') },
              { header: 'Summary', render: (row) => row.summary },
              { header: 'Status', render: (row) => <StatusPill label={inspectionStatus[row.status] ?? String(row.status)} /> },
            ]} />
          </section>
          <section className="work-section">
            <div className="section-title"><AlertTriangle size={18} aria-hidden="true" /><h3>Crop issues</h3></div>
            <DataTable rows={issues} emptyMessage="No crop issues found." columns={[
              { header: 'Title', render: (row) => row.title },
              { header: 'Severity', render: (row) => <StatusPill label={issueSeverity[row.severity] ?? String(row.severity)} tone={row.severity >= 3 ? 'bad' : 'warn'} /> },
              { header: 'Status', render: (row) => <StatusPill label={issueStatus[row.status] ?? String(row.status)} tone={row.status === 2 ? 'bad' : 'neutral'} /> },
            ]} />
          </section>
          <section className="work-section compact-section">
            <div className="section-title"><Camera size={18} aria-hidden="true" /><h3>Cloudinary image upload</h3></div>
            <p className="muted-text">The API endpoint is wired at POST /api/inspections/:id/images. Real uploads require Cloudinary values in the backend environment.</p>
            <p className="muted-text">Observations tracked: {observations.length}</p>
          </section>
        </>
      )}
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\InspectionsPage.tsx") -Encoding UTF8

@'
import { FormEvent, useEffect, useMemo, useState } from 'react'
import { Package, Warehouse } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { FormPanel, SelectInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { reservationStatus } from '../labels'
import type { InventoryStock, PagedResult, Reservation, ResourceCategory, ResourceItem, Supplier } from '../types'

export function ResourcesPage() {
  const [categories, setCategories] = useState<ResourceCategory[]>([])
  const [suppliers, setSuppliers] = useState<Supplier[]>([])
  const [resources, setResources] = useState<ResourceItem[]>([])
  const [stocks, setStocks] = useState<InventoryStock[]>([])
  const [reservations, setReservations] = useState<Reservation[]>([])
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [categoryForm, setCategoryForm] = useState({ name: '', description: '' })
  const [resourceForm, setResourceForm] = useState({ resourceCategoryId: '', supplierId: '', name: '', unit: '' })
  const [stockForm, setStockForm] = useState({ resourceId: '', quantityOnHand: '', lowStockThreshold: '' })
  const [reservationForm, setReservationForm] = useState({ inventoryStockId: '', quantity: '', purpose: '' })

  const categoryOptions = categories.map((category) => ({ value: category.id, label: category.name }))
  const supplierOptions = suppliers.map((supplier) => ({ value: supplier.id, label: supplier.name }))
  const resourceOptions = resources.map((resource) => ({ value: resource.id, label: resource.name }))
  const stockOptions = stocks.map((stock) => ({ value: stock.id, label: `${resourceOptions.find((item) => item.value === stock.resourceId)?.label ?? stock.resourceId.slice(0, 8)} (${stock.availableQuantity} available)` }))
  const resourceNameById = useMemo(() => new Map(resources.map((resource) => [resource.id, resource.name])), [resources])

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [categoryResult, supplierResult, resourceResult, stockResult] = await Promise.all([
        api.get<PagedResult<ResourceCategory>>('/resources/categories', { params: { sortBy: 'name' } }),
        api.get<PagedResult<Supplier>>('/resources/suppliers', { params: { sortBy: 'name' } }),
        api.get<PagedResult<ResourceItem>>('/resources', { params: { sortBy: 'name' } }),
        api.get<PagedResult<InventoryStock>>('/resources/stocks', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setCategories(categoryResult.data.items)
      setSuppliers(supplierResult.data.items)
      setResources(resourceResult.data.items)
      setStocks(stockResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  async function createCategory(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/resources/categories', categoryForm)
    setCategoryForm({ name: '', description: '' })
    await loadData()
  }

  async function createResource(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/resources', { ...resourceForm, supplierId: resourceForm.supplierId || null, isActive: true })
    setResourceForm({ resourceCategoryId: '', supplierId: '', name: '', unit: '' })
    await loadData()
  }

  async function upsertStock(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/resources/stocks', {
      resourceId: stockForm.resourceId,
      quantityOnHand: Number(stockForm.quantityOnHand),
      lowStockThreshold: Number(stockForm.lowStockThreshold),
    })
    setStockForm({ resourceId: '', quantityOnHand: '', lowStockThreshold: '' })
    await loadData()
  }

  async function reserve(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    const response = await api.post<Reservation>('/resources/reservations', {
      inventoryStockId: reservationForm.inventoryStockId,
      quantity: Number(reservationForm.quantity),
      purpose: reservationForm.purpose,
    })
    setReservations((current) => [response.data, ...current])
    setReservationForm({ inventoryStockId: '', quantity: '', purpose: '' })
    await loadData()
  }

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>Resources</p>
          <h2>Categories, suppliers, stock, and reservations</h2>
        </div>
      </header>
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <>
          <div className="work-grid">
            <FormPanel title="Add category" onSubmit={createCategory} submitLabel="Create category">
              <TextInput label="Name" value={categoryForm.name} required onChange={(value) => setCategoryForm({ ...categoryForm, name: value })} />
              <TextInput label="Description" value={categoryForm.description} onChange={(value) => setCategoryForm({ ...categoryForm, description: value })} />
            </FormPanel>
            <FormPanel title="Add resource" onSubmit={createResource} submitLabel="Create resource">
              <SelectInput label="Category" value={resourceForm.resourceCategoryId} required options={categoryOptions} onChange={(value) => setResourceForm({ ...resourceForm, resourceCategoryId: value })} />
              <SelectInput label="Supplier" value={resourceForm.supplierId} options={supplierOptions} onChange={(value) => setResourceForm({ ...resourceForm, supplierId: value })} />
              <TextInput label="Name" value={resourceForm.name} required onChange={(value) => setResourceForm({ ...resourceForm, name: value })} />
              <TextInput label="Unit" value={resourceForm.unit} required onChange={(value) => setResourceForm({ ...resourceForm, unit: value })} />
            </FormPanel>
            <FormPanel title="Set stock" onSubmit={upsertStock} submitLabel="Save stock">
              <SelectInput label="Resource" value={stockForm.resourceId} required options={resourceOptions} onChange={(value) => setStockForm({ ...stockForm, resourceId: value })} />
              <TextInput label="On hand" value={stockForm.quantityOnHand} type="number" required onChange={(value) => setStockForm({ ...stockForm, quantityOnHand: value })} />
              <TextInput label="Low threshold" value={stockForm.lowStockThreshold} type="number" required onChange={(value) => setStockForm({ ...stockForm, lowStockThreshold: value })} />
            </FormPanel>
            <FormPanel title="Reserve stock" onSubmit={reserve} submitLabel="Reserve">
              <SelectInput label="Stock" value={reservationForm.inventoryStockId} required options={stockOptions} onChange={(value) => setReservationForm({ ...reservationForm, inventoryStockId: value })} />
              <TextInput label="Quantity" value={reservationForm.quantity} type="number" required onChange={(value) => setReservationForm({ ...reservationForm, quantity: value })} />
              <TextInput label="Purpose" value={reservationForm.purpose} required onChange={(value) => setReservationForm({ ...reservationForm, purpose: value })} />
            </FormPanel>
          </div>
          <section className="work-section">
            <div className="section-title"><Warehouse size={18} aria-hidden="true" /><h3>Inventory stock</h3></div>
            <DataTable rows={stocks} emptyMessage="No stock records found." columns={[
              { header: 'Resource', render: (row) => resourceNameById.get(row.resourceId) ?? row.resourceId.slice(0, 8) },
              { header: 'On hand', render: (row) => row.quantityOnHand },
              { header: 'Reserved', render: (row) => row.reservedQuantity },
              { header: 'Available', render: (row) => row.availableQuantity },
              { header: 'Low stock', render: (row) => <StatusPill label={row.availableQuantity <= row.lowStockThreshold ? 'Low' : 'OK'} tone={row.availableQuantity <= row.lowStockThreshold ? 'bad' : 'good'} /> },
            ]} />
          </section>
          <section className="work-section">
            <div className="section-title"><Package size={18} aria-hidden="true" /><h3>New reservations</h3></div>
            <DataTable rows={reservations} emptyMessage="No reservations created in this session." columns={[
              { header: 'Purpose', render: (row) => row.purpose },
              { header: 'Quantity', render: (row) => row.quantity },
              { header: 'Status', render: (row) => <StatusPill label={reservationStatus[row.status] ?? String(row.status)} /> },
            ]} />
          </section>
        </>
      )}
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\ResourcesPage.tsx") -Encoding UTF8

@'
import { FormEvent, useEffect, useState } from 'react'
import { Check, ClipboardList, Send } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { FormPanel, SelectInput, TextInput } from '../components/FormControls'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { scheduleStatus, taskStatus } from '../labels'
import type { ApprovalDecision, Farm, FarmTask, Field, IrrigationSchedule, PagedResult, UserProfile } from '../types'

export function TaskApprovalPage() {
  const [farms, setFarms] = useState<Farm[]>([])
  const [fields, setFields] = useState<Field[]>([])
  const [users, setUsers] = useState<UserProfile[]>([])
  const [tasks, setTasks] = useState<FarmTask[]>([])
  const [schedules, setSchedules] = useState<IrrigationSchedule[]>([])
  const [approvals, setApprovals] = useState<ApprovalDecision[]>([])
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)
  const [taskForm, setTaskForm] = useState({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: '', status: 2 })
  const [scheduleForm, setScheduleForm] = useState({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })

  const farmOptions = farms.map((farm) => ({ value: farm.id, label: farm.name }))
  const fieldOptions = fields.map((field) => ({ value: field.id, label: field.name }))
  const userOptions = users.map((user) => ({ value: user.id, label: user.fullName }))

  async function loadData() {
    setIsLoading(true)
    setError('')
    try {
      const [farmResult, fieldResult, userResult, taskResult, scheduleResult, approvalResult] = await Promise.all([
        api.get<PagedResult<Farm>>('/crop-planning/farms'),
        api.get<PagedResult<Field>>('/crop-planning/fields'),
        api.get<PagedResult<UserProfile>>('/users'),
        api.get<PagedResult<FarmTask>>('/task-approval/tasks', { params: { sortBy: 'dueAt' } }),
        api.get<PagedResult<IrrigationSchedule>>('/task-approval/schedules', { params: { sortBy: 'scheduledAt' } }),
        api.get<PagedResult<ApprovalDecision>>('/task-approval/approvals', { params: { sortBy: 'createdAt', sortDirection: 'desc' } }),
      ])
      setFarms(farmResult.data.items)
      setFields(fieldResult.data.items)
      setUsers(userResult.data.items)
      setTasks(taskResult.data.items)
      setSchedules(scheduleResult.data.items)
      setApprovals(approvalResult.data.items)
    } catch (err) {
      setError(getErrorMessage(err))
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadData()
  }, [])

  async function createTask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/task-approval/tasks', { ...taskForm, dueAt: new Date(taskForm.dueAt).toISOString() })
    setTaskForm({ farmId: '', title: '', description: '', dueAt: '', assignedToUserId: '', status: 2 })
    await loadData()
  }

  async function createSchedule(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    await api.post('/task-approval/schedules', {
      ...scheduleForm,
      scheduledAt: new Date(scheduleForm.scheduledAt).toISOString(),
      durationMinutes: Number(scheduleForm.durationMinutes),
    })
    setScheduleForm({ fieldId: '', scheduledAt: '', durationMinutes: '', notes: '', status: 1 })
    await loadData()
  }

  async function approveTask(id: string) {
    await api.post(`/task-approval/tasks/${id}/approve`, { comment: 'Approved from React console', agentWorkflowId: null })
    await loadData()
  }

  async function approveSchedule(id: string) {
    await api.post(`/task-approval/schedules/${id}/approve`, { comment: 'Approved from React console', agentWorkflowId: null })
    await loadData()
  }

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>TaskApproval</p>
          <h2>Farm work, irrigation schedules, and decisions</h2>
        </div>
      </header>
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <>
          <div className="work-grid">
            <FormPanel title="Create farm task" onSubmit={createTask} submitLabel="Create task">
              <SelectInput label="Farm" value={taskForm.farmId} required options={farmOptions} onChange={(value) => setTaskForm({ ...taskForm, farmId: value })} />
              <TextInput label="Title" value={taskForm.title} required onChange={(value) => setTaskForm({ ...taskForm, title: value })} />
              <TextInput label="Description" value={taskForm.description} required onChange={(value) => setTaskForm({ ...taskForm, description: value })} />
              <TextInput label="Due at" type="datetime-local" value={taskForm.dueAt} required onChange={(value) => setTaskForm({ ...taskForm, dueAt: value })} />
              <SelectInput label="Assigned to" value={taskForm.assignedToUserId} required options={userOptions} onChange={(value) => setTaskForm({ ...taskForm, assignedToUserId: value })} />
            </FormPanel>
            <FormPanel title="Create irrigation schedule" onSubmit={createSchedule} submitLabel="Create schedule">
              <SelectInput label="Field" value={scheduleForm.fieldId} required options={fieldOptions} onChange={(value) => setScheduleForm({ ...scheduleForm, fieldId: value })} />
              <TextInput label="Scheduled at" type="datetime-local" value={scheduleForm.scheduledAt} required onChange={(value) => setScheduleForm({ ...scheduleForm, scheduledAt: value })} />
              <TextInput label="Duration minutes" type="number" value={scheduleForm.durationMinutes} required onChange={(value) => setScheduleForm({ ...scheduleForm, durationMinutes: value })} />
              <TextInput label="Notes" value={scheduleForm.notes} required onChange={(value) => setScheduleForm({ ...scheduleForm, notes: value })} />
            </FormPanel>
          </div>
          <section className="work-section">
            <div className="section-title"><ClipboardList size={18} aria-hidden="true" /><h3>Farm tasks</h3></div>
            <DataTable rows={tasks} emptyMessage="No tasks found." columns={[
              { header: 'Title', render: (row) => row.title },
              { header: 'Due', render: (row) => row.dueAt.slice(0, 16).replace('T', ' ') },
              { header: 'Status', render: (row) => <StatusPill label={taskStatus[row.status] ?? String(row.status)} /> },
              { header: 'Action', render: (row) => <button className="table-action" type="button" onClick={() => void approveTask(row.id)}><Check size={14} aria-hidden="true" />Approve</button> },
            ]} />
          </section>
          <section className="work-section">
            <div className="section-title"><Send size={18} aria-hidden="true" /><h3>Irrigation schedules</h3></div>
            <DataTable rows={schedules} emptyMessage="No irrigation schedules found." columns={[
              { header: 'Scheduled', render: (row) => row.scheduledAt.slice(0, 16).replace('T', ' ') },
              { header: 'Duration', render: (row) => `${row.durationMinutes} min` },
              { header: 'Status', render: (row) => <StatusPill label={scheduleStatus[row.status] ?? String(row.status)} /> },
              { header: 'Action', render: (row) => <button className="table-action" type="button" onClick={() => void approveSchedule(row.id)}><Check size={14} aria-hidden="true" />Approve</button> },
            ]} />
          </section>
          <section className="work-section compact-section">
            <h3>Recorded approvals</h3>
            <p className="muted-text">{approvals.length} approval decisions returned by the API.</p>
          </section>
        </>
      )}
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\TaskApprovalPage.tsx") -Encoding UTF8

@'
import { useEffect, useState } from 'react'
import { api, getErrorMessage } from '../api/client'
import { DataTable } from '../components/DataTable'
import { ErrorState, LoadingState } from '../components/States'
import { StatusPill } from '../components/StatusPill'
import { roleLabels } from '../labels'
import type { PagedResult, UserProfile } from '../types'

export function UsersPage() {
  const [users, setUsers] = useState<UserProfile[]>([])
  const [error, setError] = useState('')
  const [isLoading, setIsLoading] = useState(true)

  useEffect(() => {
    api
      .get<PagedResult<UserProfile>>('/users', { params: { sortBy: 'fullName' } })
      .then((response) => setUsers(response.data.items))
      .catch((err) => setError(getErrorMessage(err)))
      .finally(() => setIsLoading(false))
  }, [])

  return (
    <section className="page-stack">
      <header className="page-header">
        <div>
          <p>Admin</p>
          <h2>User management</h2>
        </div>
      </header>
      {error ? <ErrorState message={error} /> : null}
      {isLoading ? <LoadingState /> : (
        <section className="work-section">
          <DataTable rows={users} emptyMessage="No users found." columns={[
            { header: 'Name', render: (row) => row.fullName },
            { header: 'Email', render: (row) => row.email },
            { header: 'Role', render: (row) => roleLabels[row.role] },
            { header: 'Status', render: (row) => <StatusPill label={row.isActive ? 'Active' : 'Inactive'} tone={row.isActive ? 'good' : 'bad'} /> },
          ]} />
        </section>
      )}
    </section>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "pages\UsersPage.tsx") -Encoding UTF8

@'
import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { AuthProvider } from './auth/AuthContext'
import { Layout } from './components/Layout'
import { ProtectedRoute } from './components/ProtectedRoute'
import { CropPlanningPage } from './pages/CropPlanningPage'
import { DashboardPage } from './pages/DashboardPage'
import { InspectionsPage } from './pages/InspectionsPage'
import { LoginPage } from './pages/LoginPage'
import { ResourcesPage } from './pages/ResourcesPage'
import { TaskApprovalPage } from './pages/TaskApprovalPage'
import { UsersPage } from './pages/UsersPage'
import './styles.css'

export function AppRoutes() {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<ProtectedRoute />}>
        <Route element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="crop-planning" element={<CropPlanningPage />} />
          <Route path="inspections" element={<InspectionsPage />} />
          <Route path="resources" element={<ResourcesPage />} />
          <Route path="task-approval" element={<TaskApprovalPage />} />
          <Route path="users" element={<UsersPage />} />
        </Route>
      </Route>
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  )
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <AppRoutes />
      </AuthProvider>
    </BrowserRouter>
  )
}
'@ | Set-Content -Path (Join-Path $srcRoot "App.tsx") -Encoding UTF8

@'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import App from './App.tsx'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
'@ | Set-Content -Path (Join-Path $srcRoot "main.tsx") -Encoding UTF8

@'
:root {
  color: #1f2a21;
  background: #f4f2ec;
  font-family: Aptos, "Segoe UI", Candara, sans-serif;
  font-synthesis: none;
  text-rendering: optimizeLegibility;
  -webkit-font-smoothing: antialiased;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
  min-width: 320px;
  min-height: 100vh;
}

button,
input,
select {
  font: inherit;
}
'@ | Set-Content -Path (Join-Path $srcRoot "index.css") -Encoding UTF8

@'
:root {
  --ink: #1f2a21;
  --field: #f4f2ec;
  --panel: #fffdf7;
  --line: #d8d1c3;
  --leaf: #1f7a4a;
  --moss: #dbe8c7;
  --gold: #b5771e;
  --clay: #9e3f31;
  --sky: #316b83;
  --shadow: 0 18px 45px rgba(34, 45, 34, 0.12);
}

.app-shell {
  min-height: 100vh;
  display: grid;
  grid-template-columns: 280px minmax(0, 1fr);
  background:
    linear-gradient(90deg, rgba(31, 122, 74, 0.08) 1px, transparent 1px),
    linear-gradient(rgba(31, 122, 74, 0.05) 1px, transparent 1px),
    var(--field);
  background-size: 38px 38px;
}

.sidebar {
  display: flex;
  min-height: 100vh;
  flex-direction: column;
  gap: 28px;
  padding: 24px;
  color: #f8f6ed;
  background: #203125;
  border-right: 1px solid rgba(255, 255, 255, 0.12);
}

.brand,
.sidebar-footer,
.section-title,
.search-box,
.state-box,
.table-action {
  display: flex;
  align-items: center;
}

.brand {
  gap: 12px;
}

.brand strong {
  display: block;
  font-size: 1.1rem;
}

.brand span,
.sidebar-footer span,
.page-header p,
.muted-text {
  color: rgba(31, 42, 33, 0.68);
}

.sidebar .brand span,
.sidebar-footer span {
  color: rgba(248, 246, 237, 0.7);
}

.sidebar nav {
  display: grid;
  gap: 8px;
}

.sidebar a {
  display: flex;
  align-items: center;
  gap: 10px;
  min-height: 44px;
  padding: 0 12px;
  color: rgba(248, 246, 237, 0.72);
  text-decoration: none;
  border-radius: 8px;
}

.sidebar a.active,
.sidebar a:hover {
  color: #fffdf7;
  background: rgba(219, 232, 199, 0.14);
}

.sidebar-footer {
  margin-top: auto;
  justify-content: space-between;
  gap: 12px;
  padding-top: 16px;
  border-top: 1px solid rgba(255, 255, 255, 0.14);
}

.icon-button,
.table-action {
  border: 1px solid rgba(255, 255, 255, 0.18);
  color: inherit;
  background: rgba(255, 255, 255, 0.08);
  border-radius: 8px;
  min-width: 38px;
  min-height: 38px;
  cursor: pointer;
}

.content {
  padding: 32px;
  overflow: auto;
}

.page-stack {
  display: grid;
  gap: 22px;
}

.page-header {
  display: flex;
  justify-content: space-between;
  gap: 18px;
  align-items: end;
}

.page-header p {
  margin: 0 0 6px;
  font-size: 0.78rem;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: 0;
}

.page-header h2 {
  margin: 0;
  font-size: clamp(1.6rem, 2.5vw, 2.4rem);
  line-height: 1.08;
}

.metric-grid {
  display: grid;
  grid-template-columns: repeat(6, minmax(132px, 1fr));
  gap: 14px;
}

.metric-card,
.form-panel,
.work-section,
.login-panel {
  background: rgba(255, 253, 247, 0.92);
  border: 1px solid var(--line);
  border-radius: 8px;
  box-shadow: var(--shadow);
}

.metric-card {
  min-height: 132px;
  display: grid;
  align-content: space-between;
  gap: 14px;
  padding: 18px;
}

.metric-card svg {
  color: var(--leaf);
}

.metric-card span {
  color: rgba(31, 42, 33, 0.67);
}

.metric-card strong {
  font-size: 2rem;
}

.work-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
  gap: 16px;
  align-items: start;
}

.work-section,
.form-panel {
  padding: 18px;
}

.compact-section {
  box-shadow: none;
}

.section-title {
  gap: 10px;
  margin-bottom: 14px;
}

.section-title h3,
.form-panel h3,
.compact-section h3 {
  margin: 0;
  font-size: 1rem;
}

.form-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
  gap: 12px;
  margin: 14px 0;
}

.field-control,
.login-form label {
  display: grid;
  gap: 6px;
  font-size: 0.88rem;
  font-weight: 700;
}

.field-control input,
.field-control select,
.login-form input,
.search-box input {
  min-height: 40px;
  width: 100%;
  padding: 0 11px;
  color: var(--ink);
  background: #fffefa;
  border: 1px solid var(--line);
  border-radius: 8px;
}

.primary-button,
.search-box button {
  min-height: 40px;
  padding: 0 16px;
  color: #fffdf7;
  background: var(--leaf);
  border: 1px solid #155f39;
  border-radius: 8px;
  cursor: pointer;
}

.primary-button:disabled {
  opacity: 0.62;
  cursor: wait;
}

.search-box {
  gap: 8px;
  min-width: min(420px, 100%);
  padding: 8px;
  background: rgba(255, 253, 247, 0.9);
  border: 1px solid var(--line);
  border-radius: 8px;
}

.table-wrap {
  width: 100%;
  overflow-x: auto;
}

table {
  width: 100%;
  border-collapse: collapse;
}

th,
td {
  padding: 12px 10px;
  text-align: left;
  border-bottom: 1px solid var(--line);
  vertical-align: middle;
  white-space: nowrap;
}

th {
  color: rgba(31, 42, 33, 0.66);
  font-size: 0.76rem;
  text-transform: uppercase;
  letter-spacing: 0;
}

.status-pill {
  display: inline-flex;
  align-items: center;
  min-height: 26px;
  padding: 0 9px;
  border-radius: 999px;
  background: #e9e4d7;
  color: var(--ink);
  font-weight: 700;
  font-size: 0.78rem;
}

.status-good {
  color: #0d5734;
  background: #dbe8c7;
}

.status-warn {
  color: #754707;
  background: #f0d79e;
}

.status-bad {
  color: #7a2117;
  background: #f1cbc5;
}

.table-action {
  gap: 6px;
  justify-content: center;
  color: var(--ink);
  background: #fffefa;
  border-color: var(--line);
}

.state-box {
  gap: 10px;
  min-height: 54px;
  padding: 14px;
  background: #fffefa;
  border: 1px dashed var(--line);
  border-radius: 8px;
}

.state-box-error,
.form-error {
  color: #7a2117;
  background: #f7ddd8;
  border-color: #d79a90;
}

.spin {
  animation: spin 1s linear infinite;
}

.login-screen {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 24px;
  background:
    linear-gradient(120deg, rgba(31, 122, 74, 0.18), transparent 48%),
    linear-gradient(45deg, rgba(49, 107, 131, 0.18), transparent 40%),
    var(--field);
}

.login-panel {
  width: min(420px, 100%);
  padding: 28px;
}

.login-mark {
  display: flex;
  gap: 14px;
  align-items: center;
  margin-bottom: 24px;
}

.login-mark h1 {
  margin: 0;
  font-size: 2rem;
}

.login-mark p {
  margin: 4px 0 0;
  color: rgba(31, 42, 33, 0.68);
}

.login-form {
  display: grid;
  gap: 14px;
}

.form-error {
  padding: 10px 12px;
  border: 1px solid #d79a90;
  border-radius: 8px;
}

.role-strip {
  display: flex;
  flex-wrap: wrap;
  gap: 10px;
}

.role-strip span {
  padding: 8px 10px;
  background: #f2eadc;
  border-radius: 8px;
}

@keyframes spin {
  to {
    transform: rotate(360deg);
  }
}

@media (max-width: 980px) {
  .app-shell {
    grid-template-columns: 1fr;
  }

  .sidebar {
    min-height: auto;
  }

  .sidebar nav {
    grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
  }

  .metric-grid {
    grid-template-columns: repeat(2, minmax(132px, 1fr));
  }
}

@media (max-width: 640px) {
  .content {
    padding: 18px;
  }

  .page-header {
    align-items: stretch;
    flex-direction: column;
  }

  .metric-grid {
    grid-template-columns: 1fr;
  }

  th,
  td {
    white-space: normal;
  }
}
'@ | Set-Content -Path (Join-Path $srcRoot "styles.css") -Encoding UTF8

@'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { getErrorMessage } from './api/client'
import { AuthContext } from './auth/AuthContext'
import { DataTable } from './components/DataTable'
import { ProtectedRoute } from './components/ProtectedRoute'
import { LoginPage } from './pages/LoginPage'

function renderLogin(login = vi.fn()) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider
        value={{
          user: null,
          token: null,
          isAuthenticated: false,
          isLoading: false,
          login,
          logout: vi.fn(),
        }}
      >
        <LoginPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  )
}

describe('React foundation', () => {
  it('validates login form before calling the API', async () => {
    const login = vi.fn()
    renderLogin(login)
    await userEvent.clear(screen.getByLabelText(/email/i))
    await userEvent.click(screen.getByRole('button', { name: /sign in/i }))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email and password are required.')
    expect(login).not.toHaveBeenCalled()
  })

  it('redirects protected routes to login when unauthenticated', () => {
    render(
      <MemoryRouter initialEntries={['/']}>
        <AuthContext.Provider
          value={{
            user: null,
            token: null,
            isAuthenticated: false,
            isLoading: false,
            login: vi.fn(),
            logout: vi.fn(),
          }}
        >
          <Routes>
            <Route element={<ProtectedRoute />}>
              <Route index element={<div>Private dashboard</div>} />
            </Route>
            <Route path="/login" element={<div>Login target</div>} />
          </Routes>
        </AuthContext.Provider>
      </MemoryRouter>,
    )

    expect(screen.getByText('Login target')).toBeInTheDocument()
  })

  it('shows empty table state', () => {
    render(<DataTable rows={[]} emptyMessage="No rows here." columns={[{ header: 'Name', render: () => 'x' }]} />)

    expect(screen.getByText('No rows here.')).toBeInTheDocument()
  })

  it('normalizes unknown API errors', () => {
    expect(getErrorMessage('bad')).toBe('Unexpected error')
  })
})
'@ | Set-Content -Path (Join-Path $srcRoot "App.test.tsx") -Encoding UTF8

Remove-Item -Force -ErrorAction SilentlyContinue (Join-Path $srcRoot "App.css")


