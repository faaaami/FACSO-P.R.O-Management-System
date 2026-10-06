import { useNavigate } from 'react-router-dom'
import {
  Building2,
  CalendarClock,
  CalendarPlus,
  ClipboardList,
  FileText,
  Hash,
  Info,
  Mail,
  MapPin,
  Phone,
  Users,
} from 'lucide-react'
import Drawer from '../../client/documents/Drawer'
import LoadingState from '../../client/LoadingState'
import ErrorState from '../../client/ErrorState'
import StatusPill from '../../client/StatusPill'
import AgentClientEntitiesSection from './AgentClientEntitiesSection'
import { useAgentClient } from '../../../hooks/agent/useAgentClient'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { formatDate } from '../documents/documentDisplay'

function DetailRow({ label, value, mono }) {
  return (
    <div className="flex items-start justify-between gap-3 py-2">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span
        className={`min-w-0 break-words text-right text-xs font-medium text-[#16181D] ${mono ? 'font-mono' : ''}`}
      >
        {value ?? <span className="text-[#9CA3AF]">N/A</span>}
      </span>
    </div>
  )
}

/**
 * Read-only client company detail drawer for the Agent portal.
 *
 * Agent visibility of a company is task-mediated (the backend requires a
 * non-deleted renewal task assigned to the caller), so this drawer never
 * describes the company as "yours" or "assigned to you".
 *
 * The drill-down actions are navigation only. The backend exposes no
 * per-client employees/documents/tasks endpoint, so no counts are shown and
 * none of those pages are pre-filtered to this company.
 */
function AgentClientDetailDrawer({ clientId, onClose }) {
  const navigate = useNavigate()
  const { data: client, isLoading, isError, error, refresh } =
    useAgentClient(clientId)

  const drillDowns = [
    { label: 'Employees', path: '/employees', icon: Users },
    { label: 'Documents', path: '/documents', icon: FileText },
    { label: 'Renewal Tasks', path: '/renewal-tasks', icon: ClipboardList },
  ]

  function goTo(path) {
    onClose()
    navigate(path)
  }

  let content
  if (isLoading) {
    content = <LoadingState label="Loading client details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message={extractApiErrorMessage(
          error,
          'Could not load this client company. It may have been removed, or it may not be visible through your assigned renewal tasks.'
        )}
        onRetry={() => refresh()}
      />
    )
  } else if (!client) {
    content = <p className="text-sm text-[#6B7280]">Client company is not available.</p>
  } else {
    content = (
      <div className="space-y-5">
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <StatusPill
            label={client.isActive ? 'Active' : 'Inactive'}
            tone={client.isActive ? 'success' : 'neutral'}
          />
          <p className="flex items-start gap-2 text-xs text-[#6B7280]">
            <Info size={13} className="mt-0.5 shrink-0" aria-hidden="true" />
            <span>
              Visible to you through assigned renewal tasks. Not a permanent
              assignment.
            </span>
          </p>
        </div>

        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
              <Building2 size={17} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-[11px] font-medium uppercase tracking-wider text-[#6B7280]">
                Company
              </p>
              <p className="mt-0.5 break-words text-sm font-semibold text-[#16181D]">
                {client.companyName}
              </p>
            </div>
          </div>
        </div>

        <div>
          <h3 className="mb-1 text-sm font-semibold text-[#16181D]">Registration</h3>
          <div className="divide-y divide-[#E2E4E9]">
            <DetailRow label="Trade licence" value={client.tradeLicenseNumber} mono />
            <DetailRow label="Emirate" value={client.emirate} />
            <DetailRow
              label="Client reference"
              value={String(client.id).slice(0, 8)}
              mono
            />
          </div>
        </div>

        <div>
          <h3 className="mb-1 text-sm font-semibold text-[#16181D]">Contact</h3>
          <div className="divide-y divide-[#E2E4E9]">
            <DetailRow
              label="Email"
              value={
                client.email ? (
                  <span className="inline-flex items-center gap-1 break-all">
                    <Mail size={12} className="shrink-0" aria-hidden="true" />
                    {client.email}
                  </span>
                ) : null
              }
            />
            <DetailRow
              label="Phone"
              value={
                client.phone ? (
                  <span className="inline-flex items-center gap-1">
                    <Phone size={12} className="shrink-0" aria-hidden="true" />
                    {client.phone}
                  </span>
                ) : null
              }
            />
            <DetailRow
              label="Address"
              value={
                client.address ? (
                  <span className="inline-flex items-start gap-1">
                    <MapPin size={12} className="mt-0.5 shrink-0" aria-hidden="true" />
                    <span className="break-words">{client.address}</span>
                  </span>
                ) : null
              }
            />
          </div>
        </div>

        <div>
          <h3 className="mb-1 text-sm font-semibold text-[#16181D]">Timestamps</h3>
          <div className="divide-y divide-[#E2E4E9]">
            <DetailRow
              label="Created"
              value={
                formatDate(client.createdAt) ? (
                  <span className="inline-flex items-center gap-1">
                    <CalendarPlus size={12} className="shrink-0" aria-hidden="true" />
                    {formatDate(client.createdAt)}
                  </span>
                ) : null
              }
            />
            <DetailRow
              label="Last updated"
              value={
                formatDate(client.updatedAt) ? (
                  <span className="inline-flex items-center gap-1">
                    <CalendarClock size={12} className="shrink-0" aria-hidden="true" />
                    {formatDate(client.updatedAt)}
                  </span>
                ) : null
              }
            />
          </div>
        </div>

        <div className="border-t border-[#E2E4E9] pt-4">
          <AgentClientEntitiesSection clientId={clientId} />
        </div>

        <div className="border-t border-[#E2E4E9] pt-4">
          <h3 className="mb-1 text-sm font-semibold text-[#16181D]">Go to</h3>
          <p className="mb-2 text-xs text-[#6B7280]">
            These open the module pages. They are not filtered to this company.
          </p>
          <div className="flex flex-col gap-2 sm:flex-row sm:flex-wrap">
            {drillDowns.map((item) => {
              const Icon = item.icon
              return (
                <button
                  key={item.path}
                  type="button"
                  onClick={() => goTo(item.path)}
                  className="inline-flex min-h-[36px] w-full items-center justify-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer sm:w-auto"
                >
                  <Icon size={13} strokeWidth={1.75} aria-hidden="true" />
                  {item.label}
                </button>
              )
            })}
          </div>
        </div>

        <p className="flex items-start gap-1.5 text-[11px] text-[#9CA3AF]">
          <Hash size={12} className="mt-0.5 shrink-0" aria-hidden="true" />
          <span>No employee, document or task counts are shown: the backend
            exposes no per-client aggregate.</span>
        </p>
      </div>
    )
  }

  return (
    <Drawer
      title="Client Details"
      icon={Building2}
      subtitle={client?.companyName ?? 'Client company'}
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default AgentClientDetailDrawer
