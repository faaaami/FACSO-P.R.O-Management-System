import {
  LayoutDashboard,
  Building2,
  Users,
  FileText,
  FileSignature,
  RefreshCw,
  ClipboardList,
  Receipt,
  UserCheck,
  Bell,
  History,
  Settings,
} from 'lucide-react'

/**
 * Single source of truth for role navigation.
 *
 * Consumed by the Sidebar and by the header search palette (its "Jump to"
 * group), so both can never drift apart.
 *
 * Note: the Admin destinations now resolve to Phase 0 page shells — routing and
 * structure only. Navigation working does not imply those screens are built.
 */
export const ADMIN_NAVIGATION = [
  // Admin's home is /admin (ROLE_HOME.Admin), so point at it directly instead of
  // relying on "/" -> HomeRedirect, otherwise the item never shows as active.
  { name: 'Dashboard', path: '/admin', icon: LayoutDashboard, exact: true },
  { name: 'Clients', path: '/clients', icon: Building2 },
  { name: 'Employees', path: '/employees', icon: Users },
  { name: 'Documents', path: '/documents', icon: FileText },
  { name: 'Renewal Tasks', path: '/renewal-tasks', icon: RefreshCw },
  { name: 'Service Requests', path: '/service-requests', icon: ClipboardList },
  { name: 'Invoices', path: '/invoices', icon: Receipt },
  { name: 'Staff', path: '/staff', icon: UserCheck },
  { name: 'Notifications', path: '/notifications', icon: Bell },
  { name: 'Audit Log', path: '/audit-log', icon: History },
  { name: 'Settings', path: '/settings', icon: Settings },
]

export const AGENT_NAVIGATION = [
  { name: 'Dashboard', path: '/agent', icon: LayoutDashboard, exact: true },
  { name: 'Clients', path: '/clients', icon: Building2 },
  { name: 'Employees', path: '/employees', icon: Users },
  { name: 'Documents', path: '/documents', icon: FileText },
  { name: 'Renewal Tasks', path: '/renewal-tasks', icon: RefreshCw },
  { name: 'Notifications', path: '/notifications', icon: Bell },
  { name: 'Settings', path: '/settings', icon: Settings },
]

export const CLIENT_NAVIGATION = [
  { name: 'Dashboard', path: '/client', icon: LayoutDashboard, exact: true },
  { name: 'Contracts', path: '/contracts', icon: FileSignature },
  { name: 'Documents', path: '/documents', icon: FileText },
  { name: 'Service Requests', path: '/service-requests', icon: ClipboardList },
  { name: 'Renewal Tasks', path: '/renewal-tasks', icon: RefreshCw },
  { name: 'Employees', path: '/employees', icon: Users },
  { name: 'Billing & Payments', path: '/billing-payments', icon: Receipt },
  { name: 'Notifications', path: '/notifications', icon: Bell },
  { name: 'Company Profile', path: '/company-profile', icon: Building2 },
  { name: 'Settings', path: '/settings', icon: Settings },
]

export const ROLE_NAVIGATION = {
  Admin: ADMIN_NAVIGATION,
  Agent: AGENT_NAVIGATION,
  Client: CLIENT_NAVIGATION,
}
