import { Routes, Route, Link } from 'react-router-dom'
import AppShell from '../components/layout/AppShell'
import ProtectedRoute from '../auth/ProtectedRoute'
import RoleRoute from '../auth/RoleRoute'
import { useAuth } from '../auth/AuthContext'
import Home from '../pages/Home'
import Login from '../pages/Login'
import ClientDashboard from '../pages/client/ClientDashboard'
import DocumentsPage from '../pages/client/DocumentsPage'
import ContractsPage from '../pages/client/ContractsPage'
import ServiceRequestsPage from '../pages/client/ServiceRequestsPage'
import RenewalTasksPage from '../pages/client/RenewalTasksPage'

function RoutePlaceholder({ title }) {
  return (
    <div>
      <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">{title}</h1>
      <p className="mt-1 text-sm text-[#6B7280]">
        FASCO PRO {title} view.
      </p>
    </div>
  )
}

function DocumentsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <DocumentsPage />
  }
  return <RoutePlaceholder title="Documents" />
}

function ContractsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <ContractsPage />
  }
  return <RoutePlaceholder title="Contracts" />
}

function ServiceRequestsRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <ServiceRequestsPage />
  }
  return <RoutePlaceholder title="Service Requests" />
}

function RenewalTasksRoute() {
  const { user } = useAuth()
  if (user?.role === 'Client') {
    return <RenewalTasksPage />
  }
  return <RoutePlaceholder title="Renewal Tasks" />
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
          <Route path="/" element={<Home />} />
          <Route path="/clients" element={<RoutePlaceholder title="Clients" />} />
          <Route path="/employees" element={<RoutePlaceholder title="Employees" />} />
          <Route path="/contracts" element={<ContractsRoute />} />
          <Route path="/documents" element={<DocumentsRoute />} />
          <Route path="/renewal-tasks" element={<RenewalTasksRoute />} />
          <Route path="/service-requests" element={<ServiceRequestsRoute />} />
          <Route path="/invoices" element={<RoutePlaceholder title="Invoices" />} />
          <Route path="/staff" element={<RoutePlaceholder title="Staff" />} />
          <Route path="/notifications" element={<RoutePlaceholder title="Notifications" />} />
          <Route path="/audit-log" element={<RoutePlaceholder title="Audit Log" />} />
          <Route path="/billing-payments" element={<RoutePlaceholder title="Billing & Payments" />} />
          <Route path="/company-profile" element={<RoutePlaceholder title="Company Profile" />} />
          <Route path="/settings" element={<RoutePlaceholder title="Settings" />} />
        </Route>

        {/* Role Entry Portals */}
        <Route path="/client" element={<RoleRoute allowedRoles={['Client']} />}>
          <Route element={<AppShell />}>
            <Route index element={<ClientDashboard />} />
          </Route>
        </Route>

        <Route path="/agent" element={<RoleRoute allowedRoles={['Agent']} />}>
          <Route element={<AppShell />}>
            <Route index element={<RoutePlaceholder title="Agent" />} />
          </Route>
        </Route>

        <Route path="/admin" element={<RoleRoute allowedRoles={['Admin']} />}>
          <Route element={<AppShell />}>
            <Route index element={<RoutePlaceholder title="Admin" />} />
          </Route>
        </Route>
      </Route>

      {/* Catch-all 404 */}
      <Route path="*" element={<NotFound />} />
    </Routes>
  )
}

export default AppRoutes