import { format } from 'date-fns'
import { AlertCircle, Building2, CheckCircle2, ClipboardList, FileText, Link2, Users } from 'lucide-react'
import Drawer from '../documents/Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import { useClientServiceRequest } from '../../../hooks/client/useClientServiceRequest'
import { SERVICE_REQUEST_STATUS, SERVICE_REQUEST_TYPES, enumLabel } from '../enumLabels'

function formatDate(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy')
}

function formatDateTime(value) {
  if (!value) return null
  const date = new Date(value)
  if (Number.isNaN(date.getTime())) return null
  return format(date, 'dd MMM yyyy, HH:mm')
}

function requestStatusTone(status) {
  if (status === 'Converted') return 'success'
  if (status === 'Rejected') return 'danger'
  return 'warning'
}

function MetaBlock({ label, children }) {
  return (
    <div className="rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">{label}</span>
      <div className="mt-1 text-sm font-semibold text-[#16181D] break-words">{children}</div>
    </div>
  )
}

function ServiceRequestDetailDrawer({ requestId, onClose, onOpenEmployeeDocuments }) {
  const { data: request, isLoading, isError, refetch } = useClientServiceRequest(requestId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading request details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this request. It may have been removed."
        onRetry={() => refetch()}
      />
    )
  } else if (!request) {
    content = (
      <EmptyState
        icon={ClipboardList}
        message="Service request is not available."
        description="The requested service request could not be retrieved."
      />
    )
  } else {
    const employeeId = request.employeeId
    const entityId = request.entityId
    const documentId = request.documentId

    content = (
      <div className="space-y-5">
        {/* Request identity */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <div className="flex flex-wrap items-center gap-2">
            <span className="rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
              {enumLabel(SERVICE_REQUEST_TYPES, request.type) ?? 'Service Request'}
            </span>
            <StatusPill
              label={enumLabel(SERVICE_REQUEST_STATUS, request.status) ?? 'Submitted'}
              tone={requestStatusTone(request.status)}
            />
          </div>
          <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">Request #{request.id}</p>
        </div>

        {/* Subject */}
        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-2.5">
            {employeeId ? (
              <>
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
                  <Users size={15} strokeWidth={1.75} aria-hidden="true" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-[#16181D]">Employee</p>
                  <p className="text-xs text-[#6B7280]">Request applies to an employee profile</p>
                </div>
                {onOpenEmployeeDocuments && (
                  <button
                    type="button"
                    onClick={() => onOpenEmployeeDocuments(employeeId)}
                    className="ml-auto inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
                  >
                    <FileText size={13} strokeWidth={2} aria-hidden="true" />
                    Documents
                  </button>
                )}
              </>
            ) : entityId ? (
              <>
                <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
                  <Building2 size={15} strokeWidth={1.75} aria-hidden="true" />
                </div>
                <div>
                  <p className="text-sm font-semibold text-[#16181D]">Entity</p>
                  <p className="text-xs text-[#6B7280]">Request applies to a company entity</p>
                </div>
              </>
            ) : (
              <p className="text-xs text-[#9CA3AF]">No subject</p>
            )}
          </div>
        </div>

        {/* Description */}
        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">Notes</span>
          <p className="mt-1 text-sm text-[#16181D] whitespace-pre-wrap break-words">
            {request.description || 'No notes provided.'}
          </p>
        </div>

        {/* Outcome blocks */}
        {request.status === 'Rejected' && (
          <div className="flex items-start gap-2 rounded-[8px] bg-red-50 p-3 text-xs font-medium text-[#DC2626] border border-red-200">
            <AlertCircle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            <div>
              <p className="font-semibold">Rejected{formatDate(request.rejectedAt) ? ` on ${formatDate(request.rejectedAt)}` : ''}</p>
              {request.rejectionReason && <p className="mt-1 font-normal text-[#9A1C1C]">{request.rejectionReason}</p>}
            </div>
          </div>
        )}

        {request.status === 'Converted' && (
          <div className="flex items-start gap-2 rounded-[8px] bg-[rgba(15,157,116,0.08)] p-3 text-xs font-medium text-[#0F9D74] border border-[#0F9D74]/20">
            <CheckCircle2 size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            <div>
              <p className="font-semibold">Converted{formatDate(request.convertedAt) ? ` on ${formatDate(request.convertedAt)}` : ''}</p>
              {request.convertedRenewalTaskId && (
                <p className="mt-1 font-normal text-[#0B7A5C]">
                  Linked renewal task: <span className="font-mono">{request.convertedRenewalTaskId}</span>
                </p>
              )}
            </div>
          </div>
        )}

        {documentId && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <div className="flex items-center gap-2 text-[#6B7280]">
              <Link2 size={13} strokeWidth={1.75} aria-hidden="true" />
              <span className="font-medium">Linked document</span>
            </div>
            <p className="mt-1.5 font-mono text-[11px] text-[#16181D] break-all">{documentId}</p>
          </div>
        )}

        {/* Timestamps */}
        <div className="grid grid-cols-2 gap-3">
          <MetaBlock label="Submitted">{formatDateTime(request.createdAt) ?? 'N/A'}</MetaBlock>
          <MetaBlock label="Last Updated">{formatDateTime(request.updatedAt) ?? 'N/A'}</MetaBlock>
          {request.status === 'Converted' && (
            <MetaBlock label="Converted At">{formatDateTime(request.convertedAt) ?? 'N/A'}</MetaBlock>
          )}
          {request.status === 'Rejected' && (
            <MetaBlock label="Rejected At">{formatDateTime(request.rejectedAt) ?? 'N/A'}</MetaBlock>
          )}
        </div>
      </div>
    )
  }

  return (
    <Drawer
      title="Service Request"
      icon={ClipboardList}
      subtitle="Submitted request on the compliance file"
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default ServiceRequestDetailDrawer