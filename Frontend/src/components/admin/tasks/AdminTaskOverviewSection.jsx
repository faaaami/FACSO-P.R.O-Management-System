import { Link } from 'react-router-dom'
import { AlertTriangle, ArrowRight, Building2, FileText, Lock, UserRound } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminDetailRow from '../clients/AdminDetailRow'
import { formatMoney } from '../../client/billing/format'
import { documentTypeLabel } from '../documents/documentDisplay'
import { useAdminEntityMaps } from '../../../hooks/admin/useAdminEntityMaps'
import {
  displayText,
  formatDate,
  formatDateTime,
  shortGuid,
  taskStatusLabel,
  taskStatusTone,
} from './taskDisplay'

/**
 * The Overview tab of the Admin renewal-task drawer.
 *
 * SOURCE: GET /api/v1/admin/tasks/{id} (GetRenewalTaskByIdResponseDto)
 *   id, status, documentId, document, clientCompanyId, assignedStaff,
 *   blockedReason, blockedSince, completedAt, stepLogCount, createdAt, updatedAt,
 *   serviceFeeAmount
 *   document      = { id, type, documentNumber, issueDate, expiryDate, fileName }
 *   assignedStaff = { id, fullName, email } | null
 *
 * `serviceFeeAmount` is a nullable decimal and is ABSENT from the list DTO, so it
 * can only be shown here, from the detail. It is rendered with the shared
 * money formatter and an explicit "Not set" for null — never as a zero, because
 * a zero would read as "the fee is nothing" when the truth is that no fee has been
 * decided yet. It is shown on the task record rather than in an invoice-shaped
 * block, because no invoice exists yet: one is created only when the task
 * completes. The editable control for it lives in AdminTaskServiceFeePanel,
 * alongside this read-only row rather than duplicating the figure.
 *
 * ------------------------------------------------------------------
 * FIELDS THE DETAIL CONTRACT DOES NOT CARRY, AND WHAT IS DONE INSTEAD.
 * ------------------------------------------------------------------
 * There is no `employeeId`, no document `fileUrl`, no service-request reference
 * and no invoice reference. Each of those absences is handled by rendering what
 * does exist, never by manufacturing a value or a link:
 *
 *   - NO EMPLOYEE. A renewal task is per DOCUMENT, and the detail names no
 *     employee. There is no employee row to show and no employee link to offer.
 *   - NO DOCUMENT FILE LINK. `document` carries `fileName` — a bare filename
 *     string, not a URL — and the Admin Documents page consumes no document
 *     query parameter, so there is no destination a "view file" action could
 *     honestly reach. The filename is displayed as metadata. An "Open document"
 *     button is therefore absent, not disabled: there is nothing to disable.
 *   - NO INVOICE. Invoicing is owned by the Admin Billing module, which lists and
 *     creates Service Fee invoices. A task can legitimately have no invoice, or
 *     one held elsewhere, and this contract carries no invoice reference — so no
 *     invoice field is rendered or implied here. The fee above is the task's own
 *     input to that future invoice, not the invoice itself.
 *
 * THE ONE OUTBOUND LINK IS /clients?search=<companyName>, and only when the
 * company name has actually been resolved. `clientCompanyId` arrives as a bare
 * Guid, so the name comes from useAdminEntityMaps. If it cannot be resolved — the
 * company is beyond that map's 1,000-row cap, or the lookup failed — the row
 * shows an explicitly unresolved short id and NO link is rendered, because a
 * search link built from a guessed or absent name would silently filter the
 * destination to nothing. Admin Employees and Admin Documents consume no such
 * parameter at all, so neither is linked.
 *
 * THE BLOCKED STATE IS SHOWN HERE AS READ-ONLY INFORMATION, AND THE CONTROL TO
 * CHANGE IT LIVES BELOW IN ITS OWN PANEL. When the detail carries a blockedReason
 * or blockedSince, a danger banner states the reason and when it began.
 *
 * That split is deliberate. This section stays a read-only report of what the
 * server holds, and the banner therefore never offers a button — but "there is
 * deliberately no Unblock control" is no longer true of the DRAWER, only of this
 * section, and the old wording had to change rather than be left to mislead.
 * PATCH /admin/tasks/{id}/unblock exists and the Admin-only control for it is
 * AdminTaskUnblockPanel, rendered immediately below this section.
 *
 * The read-only split is also what makes the post-unblock refresh honest: after a
 * successful unblock this banner disappears because the server's own detail no
 * longer carries blockedReason or blockedSince, rather than being hidden by the
 * client to fake a clean state. The cleared reason is not lost — the History tab
 * keeps the block's original row and the unblock's row beside it.
 *
 * THE STATUS IS THE SERVER'S. It is displayed verbatim with a tone from the
 * approved taskDisplay map. It is never re-derived from blockedSince,
 * completedAt or createdAt, and no state machine runs in the browser.
 *
 * `stepLogCount` is shown as a count with its provenance, because the Steps tab's
 * list deliberately does not reconcile with it — see AdminTaskStepsSection for
 * the verified SQL reason. The number is passed through untouched.
 */
function AdminTaskOverviewSection({ task }) {
  const { resolveClient } = useAdminEntityMaps()

  const company = resolveClient?.(task?.clientCompanyId)
  const document = task?.document
  const assignee = task?.assignedStaff
  const isBlocked = Boolean(task?.blockedReason || task?.blockedSince)

  return (
    <div className="flex flex-col gap-5">
      {isBlocked && (
        <div
          role="note"
          className="flex items-start gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-3"
        >
          <AlertTriangle
            size={16}
            strokeWidth={2}
            aria-hidden="true"
            className="mt-0.5 shrink-0 text-[#DC2626]"
          />
          <div className="min-w-0">
            <p className="text-sm font-semibold text-[#B91C1C]">
              This task is blocked
            </p>
            <p className="mt-0.5 break-words text-xs text-[#B91C1C]">
              {displayText(task?.blockedReason)}
            </p>
            <p className="mt-1.5 text-xs text-[#991B1B]">
              Blocked since {formatDateTime(task?.blockedSince)}. The backend
              provides no way to clear a blocked task, so this is shown for
              information only.
            </p>
          </div>
        </div>
      )}

      <section aria-labelledby="admin-task-overview-identity">
        <h3
          id="admin-task-overview-identity"
          className="mb-2 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Task
        </h3>
        <dl className="flex flex-col">
          <AdminDetailRow
            label="Status"
            value={
              <span className="inline-flex">
                <StatusPill
                  label={taskStatusLabel(task?.status) ?? 'Unknown'}
                  tone={taskStatusTone(task?.status)}
                />
              </span>
            }
          />
          <AdminDetailRow
            label="Task ID"
            value={<span className="font-mono text-[13px]">{shortGuid(task?.id) ?? displayText(null)}</span>}
            mono
          />
          <AdminDetailRow label="Created" value={formatDateTime(task?.createdAt)} />
          <AdminDetailRow label="Last updated" value={formatDateTime(task?.updatedAt)} />
          {task?.completedAt ? (
            <AdminDetailRow label="Completed" value={formatDateTime(task.completedAt)} />
          ) : null}
          <AdminDetailRow
            label="Steps recorded"
            value={
              <>
                {Number.isFinite(task?.stepLogCount)
                  ? task.stepLogCount.toLocaleString('en-US')
                  : displayText(null)}
                <span className="block text-xs text-[#6B7280]">
                  Total counted on the task record. The Steps tab may list fewer,
                  because that endpoint excludes soft-deleted steps.
                </span>
              </>
            }
          />
          {/*
            A null fee renders as an explicit "Not set", never as zero and never as
            the formatter's "N/A": a missing fee is an undecided one, and a zero
            would be a decided fee of nothing — a distinction the backend makes
            too, since it refuses `ServiceFeeAmount <= 0` before completion. The
            currency is fixed at AED in the subtext rather than being carried on
            the field, because RenewalTask.ServiceFeeAmount has no currency
            property and the invoice created at completion defaults to AED.
          */}
          <AdminDetailRow
            label="Service fee"
            value={
              Number.isFinite(task?.serviceFeeAmount) ? (
                <>
                  {formatMoney(task.serviceFeeAmount)}
                  <span className="block text-xs text-[#6B7280]">
                    Required before this task can be completed, and the amount the
                    Service Fee invoice is raised with. Editable above until the
                    task reaches Updated.
                  </span>
                </>
              ) : (
                <span className="text-[#9CA3AF]">
                  Not set
                  <span className="block text-xs">
                    No service fee has been decided for this task
                  </span>
                </span>
              )
            }
          />
        </dl>
      </section>

      <section aria-labelledby="admin-task-overview-document">
        <h3
          id="admin-task-overview-document"
          className="mb-2 flex items-center gap-2 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <FileText size={15} strokeWidth={1.75} aria-hidden="true" className="text-[#6B7280]" />
          Linked document
        </h3>
        <dl className="flex flex-col">
          <AdminDetailRow
            label="Type"
            value={document?.type ? (documentTypeLabel(document.type) ?? document.type) : displayText(null)}
          />
          <AdminDetailRow label="Document number" value={displayText(document?.documentNumber)} />
          <AdminDetailRow label="Issue date" value={formatDate(document?.issueDate)} />
          <AdminDetailRow label="Expiry date" value={formatDate(document?.expiryDate)} />
          <AdminDetailRow label="File name" value={displayText(document?.fileName)} />
          <AdminDetailRow
            label="Document ID"
            value={<span className="font-mono text-[13px]">{shortGuid(task?.documentId) ?? displayText(null)}</span>}
            mono
          />
        </dl>
        <p className="mt-2 flex items-start gap-1.5 text-xs text-[#6B7280]">
          <Lock size={12} strokeWidth={1.75} aria-hidden="true" className="mt-0.5 shrink-0" />
          <span>
            The task detail returns a file name only, not a file URL, and the
            Admin Documents list takes no document parameter — so the document is
            described here rather than opened.
          </span>
        </p>
      </section>

      <section aria-labelledby="admin-task-overview-parties">
        <h3
          id="admin-task-overview-parties"
          className="mb-2 flex items-center gap-2 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <Building2 size={15} strokeWidth={1.75} aria-hidden="true" className="text-[#6B7280]" />
          Company and assignee
        </h3>
        <dl className="flex flex-col">
          {/* Four honest outcomes, no fifth: still resolving, failed, resolved
              (a link), or unresolvable (a short id and no link). */}
          {company?.resolved ? (
            <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
              <dt className="text-xs font-medium text-[#6B7280]">Company</dt>
              <dd className="min-w-0 break-words text-sm text-[#16181D]">
                <Link
                  to={`/clients?search=${encodeURIComponent(company.name)}`}
                  className="inline-flex items-center gap-1.5 rounded-[6px] font-medium text-[#0F9D74] underline decoration-transparent transition duration-150 hover:decoration-[rgba(15,157,116,0.45)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                >
                  {company.name}
                  <ArrowRight size={13} strokeWidth={2} aria-hidden="true" />
                  <span className="sr-only"> — open in the Admin Clients list</span>
                </Link>
              </dd>
            </div>
          ) : (
            <AdminDetailRow
              label="Company"
              value={
                company ? (
                  <span className="text-[#9CA3AF]" title={String(company.id)}>
                    {company.fallback}
                    <span className="block text-xs">
                      not resolvable in Admin — no link offered
                    </span>
                  </span>
                ) : (
                  displayText(null)
                )
              }
            />
          )}

          {assignee?.fullName ? (
            <>
              <AdminDetailRow
                label="Assigned Agent"
                value={
                  <span className="inline-flex items-center gap-1.5">
                    <UserRound
                      size={13}
                      strokeWidth={1.75}
                      aria-hidden="true"
                      className="shrink-0 text-[#6B7280]"
                    />
                    {assignee.fullName}
                  </span>
                }
              />
              <AdminDetailRow label="Agent email" value={displayText(assignee.email)} />
            </>
          ) : (
            <AdminDetailRow
              label="Assigned Agent"
              value={
                <span className="text-[#9CA3AF]">
                  Unassigned
                  <span className="block text-xs">No Agent has been assigned</span>
                </span>
              }
            />
          )}
        </dl>
      </section>
    </div>
  )
}

export default AdminTaskOverviewSection
