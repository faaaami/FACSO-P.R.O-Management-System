import { Routes, Route, Link, Navigate } from 'react-router-dom'
import AppShell from '../components/layout/AppShell'
import ProtectedRoute from '../auth/ProtectedRoute'
import RoleRoute from '../auth/RoleRoute'
import { useAuth } from '../auth/AuthContext'
import Login from '../pages/Login'
import ClientDashboard from '../pages/client/ClientDashboard'
import DocumentsPage from '../pages/client/DocumentsPage'
import ContractsPage from '../pages/client/ContractsPage'
import ServiceRequestsPage from '../pages/client/ServiceRequestsPage'
import RenewalTasksPage from '../pages/client/RenewalTasksPage'
import BillingPaymentsPage from '../pages/client/BillingPaymentsPage'
import CompanyProfilePage from '../pages/client/CompanyProfilePage'
import NotificationsPage from '../pages/client/NotificationsPage'
import EmployeesPage from '../pages/client/EmployeesPage'
import SettingsPage from '../pages/client/SettingsPage'
import AgentDashboardPage from '../pages/agent/AgentDashboardPage'
import AgentClientsPage from '../pages/agent/AgentClientsPage'
import AgentDocumentsPage from '../pages/agent/AgentDocumentsPage'
import AgentEmployeesPage from '../pages/agent/AgentEmployeesPage'
import AgentNotificationsPage from '../pages/agent/AgentNotificationsPage'
import AgentRenewalTasksPage from '../pages/agent/AgentRenewalTasksPage'
import AgentSettingsPage from '../pages/agent/AgentSettingsPage'
import AdminDashboardPage from '../pages/admin/AdminDashboardPage'
import AdminClientsPage from '../pages/admin/AdminClientsPage'
import AdminDocumentsPage from '../pages/admin/AdminDocumentsPage'
import AdminEmployeesPage from '../pages/admin/AdminEmployeesPage'
import AdminNotificationsPage from '../pages/admin/AdminNotificationsPage'
import AdminServiceRequestsPage from '../pages/admin/AdminServiceRequestsPage'
import AdminTasksPage from '../pages/admin/AdminTasksPage'
import AdminStaffPage from '../pages/admin/AdminStaffPage'
import AdminInvoicesPage from '../pages/admin/AdminInvoicesPage'
import AdminAuditLogPage from '../pages/admin/AdminAuditLogPage'
import AdminSettingsPage from '../pages/admin/AdminSettingsPage'

const ROLE_HOME = { Client: '/client', Agent: '/agent', Admin: '/admin' }

function HomeRedirect() {
  const { user } = useAuth()
  return <Navigate to={ROLE_HOME[user?.role] ?? '/404'} replace />
}

/**
 * The shared-path dispatchers below each choose the page that belongs to the signed-in
 * role and, where no page exists for that role at this path, send the reader home.
 *
 * WHY A REDIRECT AND NOT A PLACEHOLDER. Reaching one of these as a role that has no
 * page here means the reader asked for something they cannot use, and the app already
 * has one answer for that: RoleRoute sends a wrong role to `/`, which HomeRedirect
 * then resolves to that role's own home. These dispatchers now answer identically, so
 * being turned away from a page behaves the same whether it was reached through a role
 * portal like /admin or through a path shared with another role. It previously rendered
 * a `RoutePlaceholder` stub inside the full AppShell, so the turn-away looked like a
 * working page rather than a refusal.
 *
 * WHAT THIS IS NOT. The `user?.role === …` branches are the actual protection and are
 * unchanged by that decision: a non-Admin never receives an Admin page component, on any
 * of the eleven Admin routes. This file decides what React paints, nothing more. The
 * backend is the only thing that decides whether a role may read or write admin data,
 * and nothing here alters that — a redirect is a navigation outcome, NOT an
 * authorization control.
 */
function DocumentsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <DocumentsPage />
  }
  if (user?.role === 'Agent') {
    return <AgentDocumentsPage />
  }
  if (user?.role === 'Admin') {
    return <AdminDocumentsPage />
  }
  return <Navigate to="/" replace />
}

function ContractsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <ContractsPage />
  }
  return <Navigate to="/" replace />
}

function ServiceRequestsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <ServiceRequestsPage />
  }
  if (user?.role === 'Admin') {
    return <AdminServiceRequestsPage />
  }
  return <Navigate to="/" replace />
}

function RenewalTasksRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <RenewalTasksPage />
  }
  if (user?.role === 'Agent') {
    return <AgentRenewalTasksPage />
  }
  if (user?.role === 'Admin') {
    return <AdminTasksPage />
  }
  return <Navigate to="/" replace />
}

function BillingPaymentsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <BillingPaymentsPage />
  }
  return <Navigate to="/" replace />
}

function CompanyProfileRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <CompanyProfilePage />
  }
  return <Navigate to="/" replace />
}

function NotificationsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <NotificationsPage />
  }
  if (user?.role === 'Agent') {
    return <AgentNotificationsPage />
  }
  if (user?.role === 'Admin') {
    return <AdminNotificationsPage />
  }
  return <Navigate to="/" replace />
}

function EmployeesRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <EmployeesPage />
  }
  if (user?.role === 'Agent') {
    return <AgentEmployeesPage />
  }
  if (user?.role === 'Admin') {
    return <AdminEmployeesPage />
  }
  return <Navigate to="/" replace />
}

function SettingsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <SettingsPage />
  }
  if (user?.role === 'Agent') {
    return <AgentSettingsPage />
  }
  if (user?.role === 'Admin') {
    return <AdminSettingsPage />
  }
  return <Navigate to="/" replace />
}

function AgentClientsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Agent') {
    return <AgentClientsPage />
  }
  if (user?.role === 'Admin') {
    return <AdminClientsPage />
  }
  return <Navigate to="/" replace />
}

// The three routes below were Admin-only paths before Phase 0. They resolve to the
// Admin page for Admin and turn every other role away, the same as the shared paths.
function InvoicesRoute() {
  const { user } = useAuth()
  if (user?.role === 'Admin') {
    return <AdminInvoicesPage />
  }
  return <Navigate to="/" replace />
}

function StaffRoute() {
  const { user } = useAuth()
  if (user?.role === 'Admin') {
    return <AdminStaffPage />
  }
  return <Navigate to="/" replace />
}

function AuditLogRoute() {
  const { user } = useAuth()
  if (user?.role === 'Admin') {
    return <AdminAuditLogPage />
  }
  return <Navigate to="/" replace />
}

function NotFound() {
  return (
    <div className="min-h-screen bg-[#F7F8FA] flex flex-col items-center justify-center px-4 text-center">
      <p className="text-5xl font-bold tracking-tight text-[#16181D]">404</p>
      <p className="mt-2 text-sm font-medium text-[#6B7280]">Page not found</p>
      <Link
        to="/"
        className="mt-4 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150"
      >
        Back to Dashboard
      </Link>
    </div>
  )
}

function AppRoutes() {
  return (
    <Routes>
      {/* Standalone Login Route */}
      <Route path="/login" element={<Login />} />

      {/* Authenticated Application Routes */}
      <Route element={<ProtectedRoute />}>
        {/* Shell Layout Routes */}
        <Route element={<AppShell />}>
          <Route path="/" element={<HomeRedirect />} />
          <Route path="/clients" element={<AgentClientsRoute />} />
          <Route path="/employees" element={<EmployeesRoute />} />
          <Route path="/contracts" element={<ContractsRoute />} />
          <Route path="/documents" element={<DocumentsRoute />} />
          <Route path="/renewal-tasks" element={<RenewalTasksRoute />} />
          <Route path="/service-requests" element={<ServiceRequestsRoute />} />
          <Route path="/invoices" element={<InvoicesRoute />} />
          <Route path="/staff" element={<StaffRoute />} />
          <Route path="/notifications" element={<NotificationsRoute />} />
          <Route path="/audit-log" element={<AuditLogRoute />} />
          <Route path="/billing-payments" element={<BillingPaymentsRoute />} />
          <Route path="/company-profile" element={<CompanyProfileRoute />} />
          <Route path="/settings" element={<SettingsRoute />} />
        </Route>

        {/* Role Entry Portals */}
        <Route path="/client" element={<RoleRoute allowedRoles={['Client']} />}>
          <Route element={<AppShell />}>
            <Route index element={<ClientDashboard />} />
          </Route>
        </Route>

        <Route path="/agent" element={<RoleRoute allowedRoles={['Agent']} />}>
          <Route element={<AppShell />}>
            <Route index element={<AgentDashboardPage />} />
          </Route>
        </Route>

        <Route path="/admin" element={<RoleRoute allowedRoles={['Admin']} />}>
          <Route element={<AppShell />}>
            <Route index element={<AdminDashboardPage />} />
          </Route>
        </Route>

        {/*
          Catch-all 404 — INSIDE ProtectedRoute, but outside AppShell.
          NotFound paints its own full-screen layout, so it must not be nested
          in the shell.

          Why it sits inside the gate: this used to be declared after the closing
          </Route> of ProtectedRoute, which meant ANY unknown path rendered a
          full 404 page to a completely unauthenticated visitor — no login
          redirect, no shell, including for admin-looking paths such as
          /admin/tasks that do not exist. Inside ProtectedRoute the outcomes are:
            logged out + unknown path -> /login
            logged in  + unknown path -> 404
          This is a navigation outcome only; it is not an authorization control.
        */}
        <Route path="*" element={<NotFound />} />
      </Route>
    </Routes>
  )
}

export default AppRoutes
