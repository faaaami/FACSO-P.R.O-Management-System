import { format } from 'date-fns'
import { AlertTriangle, CheckCircle2, FileText, Link2, RefreshCw, User } from 'lucide-react'
import Drawer from '../documents/Drawer'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import RenewalTaskHistory from './RenewalTaskHistory'
import { useClientRenewalTask } from '../../../hooks/client/useClientRenewalTask'
import { RENEWAL_TASK_STATUS, DOCUMENT_TYPES, enumLabel } from '../enumLabels'

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

function taskStatusTone(status) {
  if (status === 'Blocked') return 'danger'
  if (status === 'Approved' || status === 'Updated') return 'success'
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

function DetailRow({ label, value, mono }) {
  return (
    <div className="flex items-start justify-between gap-3">
      <span className="shrink-0 text-xs text-[#6B7280]">{label}</span>
      <span className={`text-xs font-medium text-[#16181D] break-words text-right ${mono ? 'font-mono' : ''}`}>
        {value ?? <span className="text-[#9CA3AF]">N/A</span>}
      </span>
    </div>
  )
}

function RenewalTaskDetailDrawer({ taskId, onClose, onOpenDocument }) {
  const { data: task, isLoading, isError, refetch } = useClientRenewalTask(taskId)

  let content

  if (isLoading) {
    content = <LoadingState label="Loading task details…" />
  } else if (isError) {
    content = (
      <ErrorState
        message="Could not load this task. It may have been removed."
        onRetry={() => refetch()}
      />
    )
  } else if (!task) {
    content = (
      <EmptyState
        icon={RefreshCw}
        message="Renewal task is not available."
        description="The requested renewal task could not be retrieved."
      />
    )
  } else {
    const doc = task.document

    content = (
      <div className="space-y-5">
        {/* Task identity */}
        <div className="flex flex-col gap-3 rounded-[10px] bg-[#F7F8FA] border border-[#E2E4E9] p-4">
          <div className="flex flex-wrap items-center gap-2">
            <StatusPill
              label={enumLabel(RENEWAL_TASK_STATUS, task.status) ?? 'Pending'}
              tone={taskStatusTone(task.status)}
            />
          </div>
          <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">Task #{task.id}</p>
        </div>

        {/* Blocked alert */}
        {task.status === 'Blocked' && (
          <div className="flex items-start gap-2 rounded-[8px] bg-red-50 p-3 text-xs font-medium text-[#DC2626] border border-red-200">
            <AlertTriangle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            <div>
              <p className="font-semibold">
                Action required{task.blockedSince && formatDate(task.blockedSince) ? ` since ${formatDate(task.blockedSince)}` : ''}
              </p>
              {task.blockedReason && <p className="mt-1 font-normal text-[#9A1C1C]">{task.blockedReason}</p>}
            </div>
          </div>
        )}

        {task.status === 'Approved' && task.completedAt && (
          <div className="flex items-start gap-2 rounded-[8px] bg-[rgba(15,157,116,0.08)] p-3 text-xs font-medium text-[#0F9D74] border border-[#0F9D74]/20">
            <CheckCircle2 size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
            <div>
              <p className="font-semibold">Completed{formatDate(task.completedAt) ? ` on ${formatDate(task.completedAt)}` : ''}</p>
            </div>
          </div>
        )}

        {/* Document */}
        {doc && (
          <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3 text-xs">
            <div className="flex items-center gap-2 text-[#6B7280]">
              <FileText size={13} strokeWidth={1.75} aria-hidden="true" />
              <span className="font-medium">Document in renewal</span>
              {task.documentId && onOpenDocument && (
                <button
                  type="button"
                  onClick={() => onOpenDocument(task.documentId)}
                  className="ml-auto inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
                >
                  <Link2 size={13} strokeWidth={2} aria-hidden="true" />
                  View document
                </button>
              )}
            </div>
            <div className="mt-2.5 space-y-1.5">
              <DetailRow label="Type" value={enumLabel(DOCUMENT_TYPES, doc.type)} />
              <DetailRow label="Document #" value={doc.documentNumber} mono />
              <DetailRow label="Issue date" value={formatDate(doc.issueDate)} />
              <DetailRow label="Expiry date" value={formatDate(doc.expiryDate)} />
              <DetailRow label="File" value={doc.fileName} />
            </div>
          </div>
        )}

        {/* Assigned staff */}
        <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
          <div className="flex items-center gap-2.5">
            <div className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#6B7280]">
              <User size={15} strokeWidth={1.75} aria-hidden="true" />
            </div>
            <div className="min-w-0">
              <p className="text-xs font-medium text-[#6B7280] uppercase tracking-wider">Assigned staff</p>
              {task.assignedStaff ? (
                <div className="mt-0.5 text-sm text-[#16181D]">
                  <p className="font-semibold truncate">{task.assignedStaff.fullName}</p>
                  <p className="text-xs text-[#6B7280] break-words">{task.assignedStaff.email}</p>
                </div>
              ) : (
                <p className="mt-0.5 text-sm text-[#9CA3AF]">Unassigned</p>
              )}
            </div>
          </div>
        </div>

        {/* Key metadata */}
        <div className="grid grid-cols-2 gap-3">
          <MetaBlock label="Step Log">{(task.stepLogCount ?? 0) === 1 ? '1 step' : `${task.stepLogCount ?? 0} steps`}</MetaBlock>
          {task.status === 'Blocked' && task.blockedSince ? (
            <MetaBlock label="Blocked Since">{formatDateTime(task.blockedSince) ?? 'N/A'}</MetaBlock>
          ) : task.completedAt ? (
            <MetaBlock label="Completed">{formatDateTime(task.completedAt) ?? 'N/A'}</MetaBlock>
          ) : (
            <MetaBlock label="Completion">Not yet completed</MetaBlock>
          )}
          <MetaBlock label="Created">{formatDateTime(task.createdAt) ?? 'N/A'}</MetaBlock>
          <MetaBlock label="Last Updated">{formatDateTime(task.updatedAt) ?? 'N/A'}</MetaBlock>
        </div>

        {/* History */}
        <RenewalTaskHistory taskId={taskId} />
      </div>
    )
  }

  return (
    <Drawer
      title="Renewal Task"
      icon={RefreshCw}
      subtitle="Document renewal on the compliance file"
      onClose={onClose}
    >
      {content}
    </Drawer>
  )
}

export default RenewalTaskDetailDrawer