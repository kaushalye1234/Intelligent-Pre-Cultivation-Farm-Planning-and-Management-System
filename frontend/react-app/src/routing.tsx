import {
  BarChart3,
  ClipboardCheck,
  ClipboardList,
  Leaf,
  Package,
  ShieldCheck,
  Sprout,
  Users,
} from 'lucide-react'
import type { LucideIcon } from 'lucide-react'
import type { ApplicationRole } from './types'

export const Roles = {
  Farmer: 1,
  FieldOfficer: 2,
  ResourceOfficer: 3,
  AgriculturalOfficer: 4,
  Admin: 5,
} as const

export const staffRoles: ApplicationRole[] = [
  Roles.FieldOfficer,
  Roles.ResourceOfficer,
  Roles.AgriculturalOfficer,
  Roles.Admin,
]

export function getDashboardPath(role?: ApplicationRole | null) {
  switch (role) {
    case Roles.Admin:
      return '/admin/dashboard'
    case Roles.FieldOfficer:
      return '/inspections/dashboard'
    case Roles.ResourceOfficer:
      return '/resources/dashboard'
    case Roles.AgriculturalOfficer:
      return '/officer/dashboard'
    default:
      return '/'
  }
}

export function isDecisionRole(role?: ApplicationRole | null) {
  return role === Roles.Admin || role === Roles.AgriculturalOfficer
}

export function canManageResources(role?: ApplicationRole | null) {
  return role === Roles.Admin || role === Roles.ResourceOfficer
}

export function canManageFieldOperations(role?: ApplicationRole | null) {
  return role === Roles.Admin || role === Roles.FieldOfficer || role === Roles.AgriculturalOfficer
}

export function canManageCropPlanning(role?: ApplicationRole | null) {
  return role === Roles.Admin || role === Roles.AgriculturalOfficer
}

export type NavigationItem = {
  to: string
  label: string
  icon: LucideIcon
  roles: ApplicationRole[]
}

export type NavigationGroup = {
  label: string
  items: NavigationItem[]
}

const navGroups: NavigationGroup[] = [
  {
    label: 'Overview',
    items: [
      {
        to: '/dashboard',
        label: 'Dashboard',
        icon: BarChart3,
        roles: staffRoles,
      },
    ],
  },
  {
    label: 'Operations',
    items: [
      {
        to: '/crop-planning',
        label: 'Crop Planning',
        icon: Sprout,
        roles: [Roles.Admin, Roles.AgriculturalOfficer],
      },
      {
        to: '/inspections',
        label: 'Inspections',
        icon: ClipboardCheck,
        roles: [Roles.Admin, Roles.FieldOfficer, Roles.AgriculturalOfficer],
      },
      {
        to: '/resources',
        label: 'Resources',
        icon: Package,
        roles: [Roles.Admin, Roles.ResourceOfficer],
      },
      {
        to: '/task-approval',
        label: 'Tasks & Approvals',
        icon: ShieldCheck,
        roles: [Roles.Admin, Roles.FieldOfficer, Roles.AgriculturalOfficer],
      },
    ],
  },
  {
    label: 'Administration',
    items: [
      {
        to: '/users',
        label: 'Users',
        icon: Users,
        roles: [Roles.Admin],
      },
    ],
  },
]

export function getNavigationGroups(role?: ApplicationRole | null): NavigationGroup[] {
  if (!role) return []

  return navGroups
    .map((group) => ({
      ...group,
      items: group.items.map((item) => (item.to === '/dashboard' ? { ...item, to: getDashboardPath(role) } : item)).filter((item) => item.roles.includes(role)),
    }))
    .filter((group) => group.items.length > 0)
}

export function getSectionContext(pathname: string) {
  if (pathname.includes('/crop-planning')) return { section: 'Operations', title: 'Crop Planning' }
  if (pathname.includes('/inspections') && !pathname.includes('/dashboard')) return { section: 'Operations', title: 'Inspections' }
  if (pathname.includes('/resources') && !pathname.includes('/dashboard')) return { section: 'Operations', title: 'Resources' }
  if (pathname.includes('/task-approval')) return { section: 'Operations', title: 'Tasks & Approvals' }
  if (pathname.includes('/users')) return { section: 'Administration', title: 'User Management' }
  if (pathname.includes('/dashboard') || pathname === '/dashboard') return { section: 'Overview', title: 'Dashboard' }
  return { section: 'Operations', title: 'Dashboard' }
}

export const portalIcon = Leaf
export const taskIcon = ClipboardList
