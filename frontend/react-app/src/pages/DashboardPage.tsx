import { useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Activity, AlertTriangle, ClipboardList, PackageCheck, ShieldCheck, Sprout, Users } from 'lucide-react'
import { api, getErrorMessage } from '../api/client'
import { useAuth } from '../auth/AuthContext'
import { EmptyState, ErrorState, LoadingState } from '../components/States'
import { MetricCard, PageHeader } from '../components/Ui'
import { roleLabels } from '../labels'
import { Roles } from '../routing'
import type { DashboardSummary } from '../types'

type DashboardMetric = {
  label: string
  value: number
  description: string
  icon?: ReactNode
  tone?: 'neutral' | 'good' | 'warn' | 'bad'
}

function buildMetrics(role: number | undefined, summary: DashboardSummary) {
  const allMetrics: DashboardMetric[] = [
    { label: 'Active farms', value: summary.activeFarms, description: 'Farms available in the operations system.', icon: <Sprout size={20} aria-hidden="true" /> },
    { label: 'Active crop plans', value: summary.activeCropPlans, description: 'Crop planning records currently active.', icon: <Activity size={20} aria-hidden="true" /> },
    { label: 'Open crop issues', value: summary.openCropIssues, description: 'Reported crop issues not yet closed.', icon: <AlertTriangle size={20} aria-hidden="true" />, tone: summary.openCropIssues > 0 ? 'warn' : 'neutral' },
    { label: 'Low stock resources', value: summary.lowStockResources, description: 'Inventory records at or below threshold.', icon: <PackageCheck size={20} aria-hidden="true" />, tone: summary.lowStockResources > 0 ? 'bad' : 'neutral' },
    { label: 'Pending tasks', value: summary.pendingTasks, description: 'Farm tasks waiting for action.', icon: <ClipboardList size={20} aria-hidden="true" />, tone: summary.pendingTasks > 0 ? 'warn' : 'neutral' },
    { label: 'Pending approvals', value: summary.pendingApprovals, description: 'Manual approvals awaiting decision.', icon: <ShieldCheck size={20} aria-hidden="true" />, tone: summary.pendingApprovals > 0 ? 'warn' : 'neutral' },
  ]

  if (role === Roles.FieldOfficer) {
    return [allMetrics[2], allMetrics[0], allMetrics[4], allMetrics[1]]
  }

  if (role === Roles.ResourceOfficer) {
    return [allMetrics[3], allMetrics[0], allMetrics[5], allMetrics[4]]
  }

  if (role === Roles.AgriculturalOfficer) {
    return [allMetrics[5], allMetrics[4], allMetrics[1], allMetrics[2]]
  }

  return allMetrics
}

function getDashboardCopy(role: number | undefined, name?: string) {
  if (role === Roles.FieldOfficer) {
    return {
      title: 'Inspection Dashboard',
      description: 'Field visits, open crop issues and inspection workload using current backend data.',
      greeting: `Good day${name ? `, ${name}` : ''}. Here is your field operations overview.`,
    }
  }

  if (role === Roles.ResourceOfficer) {
    return {
      title: 'Resource Dashboard',
      description: 'Inventory attention, reservations context and resource operations using current backend data.',
      greeting: `Good day${name ? `, ${name}` : ''}. Here is your resource operations overview.`,
    }
  }

  if (role === Roles.AgriculturalOfficer) {
    return {
      title: 'Agricultural Officer Dashboard',
      description: 'Plans, tasks, schedules and approvals requiring officer review.',
      greeting: `Good day${name ? `, ${name}` : ''}. Here is your approval overview.`,
    }
  }

  return {
    title: 'Admin Dashboard',
    description: 'A full operational overview across farms, plans, inspections, resources, tasks and users.',
    greeting: `Good day${name ? `, ${name}` : ''}. Here is the platform overview.`,
  }
}

export function DashboardPage() {
  const { user } = useAuth()
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
  if (!summary) return <EmptyState title="Dashboard unavailable" message="No dashboard summary was returned by the API." />

  const copy = getDashboardCopy(user?.role, user?.fullName)
  const metrics = buildMetrics(user?.role, summary)
  const attentionItems = [
    { label: 'Open crop issues', value: summary.openCropIssues },
    { label: 'Low stock resources', value: summary.lowStockResources },
    { label: 'Pending tasks', value: summary.pendingTasks },
    { label: 'Pending approvals', value: summary.pendingApprovals },
  ].filter((item) => item.value > 0)

  return (
    <section className="page-stack">
      <PageHeader eyebrow="Operational snapshot" title={copy.title} description={copy.description} />

      <section className="dashboard-intro">
        <h2>{copy.greeting}</h2>
        <p>All figures shown here come from the existing dashboard summary API.</p>
      </section>

      <div className="metric-grid">
        {metrics.map((metric) => (
          <MetricCard key={metric.label} label={metric.label} value={metric.value} description={metric.description} icon={metric.icon} tone={metric.tone} />
        ))}
      </div>

      <section className="work-section dashboard-section">
        <div className="section-title">
          <AlertTriangle size={18} aria-hidden="true" />
          <h2>Operational Attention</h2>
        </div>
        {attentionItems.length > 0 ? (
          <div className="attention-list">
            {attentionItems.map((item) => (
              <article key={item.label}>
                <span>{item.label}</span>
                <strong>{item.value}</strong>
              </article>
            ))}
          </div>
        ) : (
          <EmptyState title="No attention items" message="The summary API did not return any open issues, low-stock records, pending tasks or pending approvals." />
        )}
      </section>

      {user?.role === Roles.Admin ? (
        <section className="work-section dashboard-section">
          <div className="section-title">
            <Users size={18} aria-hidden="true" />
            <h2>Users by Role</h2>
          </div>
          <div className="role-strip">
            {summary.usersByRole.map((roleCount) => (
              <span key={roleCount.role}>
                {roleLabels[Number(roleCount.role) as keyof typeof roleLabels] ?? roleCount.role}: <strong>{roleCount.count}</strong>
              </span>
            ))}
          </div>
        </section>
      ) : null}
    </section>
  )
}
