import type { AdminRole, Tab } from '../types'

const operationalRoles: AdminRole[] = ['HRAdmin', 'WorkforceAdmin']

export function parseAdminRoles(role: string | null | undefined): AdminRole[] {
  if (!role?.trim()) return ['HRAdmin']

  const roles = new Set<AdminRole>()
  for (const part of role.split(/[,;|]/).map((item) => item.trim()).filter(Boolean)) {
    if (part.toLowerCase() === 'owner') return ['Owner']
    if (part.toLowerCase() === 'admin') {
      roles.add('HRAdmin')
      roles.add('WorkforceAdmin')
      continue
    }
    if (part.toLowerCase() === 'hradmin') roles.add('HRAdmin')
    if (part.toLowerCase() === 'workforceadmin') roles.add('WorkforceAdmin')
  }

  return roles.size === 0 ? ['HRAdmin'] : operationalRoles.filter((item) => roles.has(item))
}

export function hasAdminRole(role: string | null | undefined, expected: AdminRole) {
  return parseAdminRoles(role).includes(expected)
}

export function canAccessTab(role: string | null | undefined, tab: Tab) {
  if (hasAdminRole(role, 'Owner')) return true
  if (tab === 'overview') return true
  if (hasAdminRole(role, 'HRAdmin') && (tab === 'campaigns' || tab === 'responses')) return true
  if (hasAdminRole(role, 'WorkforceAdmin') && (tab === 'agents' || tab === 'activity')) return true
  return false
}

export function getAllowedTabs(role: string | null | undefined): Tab[] {
  const tabs: Tab[] = ['overview', 'campaigns', 'agents', 'activity', 'responses', 'admins', 'lt', 'settings']
  return tabs.filter((tab) => canAccessTab(role, tab))
}

export function formatAdminRoles(role: string | null | undefined) {
  const labels: Record<AdminRole, string> = {
    Owner: 'Owner',
    HRAdmin: 'HR Admin',
    WorkforceAdmin: 'Workforce Admin',
  }

  return parseAdminRoles(role).map((item) => labels[item]).join(', ')
}
