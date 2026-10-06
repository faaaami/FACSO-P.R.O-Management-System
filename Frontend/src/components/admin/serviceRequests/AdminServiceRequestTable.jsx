import { Building2, Eye, User, Users } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  formatDate,
  requestDescriptionPreview,
  serviceRequestStatusText,
  serviceRequestStatusTone,
  serviceRequestTypeText,
  shortGuid,
  subjectKindText,
} from './serviceRequestDisplay'

/**
 * Admin service-request list.
 *
 * RESPONSIVE: a real <table> from `md` up inside an overflow container, and a
 * <ul> card list below it. The table needs a min-width because six columns of
 * wrapped text cannot compress further, so below `md` it is replaced rather than
 * squeezed — a horizontally scrolling table is unusable on a phone.
 *
 * WHAT THE COLUMNS SHOW, AND WHY NOT MORE. RenewalTaskListItemDto-style guidance
 * applies: only fields the DTO actually carries are rendered. There is no
 * assignee, no document name, no entity name and no outcome reason column,
 * because none of those exist on ServiceRequestListItemDto. Every entity
 * reference arrives as a bare Guid and is resolved by the resolvers this component
 * receives as props, because a table renders many rows and cannot call a hook per
 * row.
 *
 * THE OUTCOME COLUMN IS A COLLAPSE, NOT A SUMMARY. Converted and Rejected are
 * mutually exclusive terminal states, so one cell renders whichever timestamp the
 * DTO carries plus the rejection reason when there is one. A converted request
 * also carries convertedRenewalTaskId; the full id is deliberately NOT shown
 * here — the drawer reports it in full — and the row instead says the request was
 * converted, because a truncated Guid in a dense table is noise. Nothing about
 * the converted task is linkable from here.
 *
 * THE SUBJECT CELL NAMES A PERSON OR AN ORGANISATION AND SAYS WHICH. The backend's
 * CK_service_requests_subject constraint guarantees at least one of employeeId /
 * entityId, and a request may carry both. An employee is resolved from the shared
 * employee index; an entity is not resolved in a list at all, because doing so
 * would mean walking every company and then each company's entities — an
 * unbounded N+1. An unresolved reference is therefore shown as a short id that
 * the copy explicitly labels as unresolved, never as a name.
 */
function SubjectCell({ request, resolveEmployee, resolveCompany }) {
  const employee = resolveEmployee(request.employeeId)
  const company = resolveCompany(request.clientCompanyId)
  const kind = subjectKindText(request)

  return (
    <div className="min-w-0">
      {employee.kind === 'employee' ? (
        <p className="flex min-w-0 items-center gap-1.5 text-[#16181D]">
          <User size={12} strokeWidth={1.75} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
          <span className="min-w-0 break-words">
            {employee.resolved ? (
              employee.name
            ) : (
              <span className="text-[#9CA3AF]" title={String(employee.id)}>
                Unresolved employee &middot; {shortGuid(employee.id)}
              </span>
            )}
          </span>
        </p>
      ) : (
        <p className="flex min-w-0 items-center gap-1.5 text-[#16181D]">
          <Building2 size={12} strokeWidth={1.75} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
          <span className="min-w-0 break-words">
            {request.entityId ? (
              <span className="text-[#9CA3AF]" title={String(request.entityId)}>
                Unresolved entity &middot; {shortGuid(request.entityId)}
              </span>
            ) : (
              <span className="text-[#9CA3AF]">No subject</span>
            )}
          </span>
        </p>
      )}

      <p className="mt-0.5 min-w-0 break-words text-xs text-[#6B7280]">
        {company?.resolved ? (
          company.name
        ) : (
          <span className="text-[#9CA3AF]">
            Unresolved company &middot; {shortGuid(request.clientCompanyId)}
          </span>
        )}
      </p>

      {kind && <span className="sr-only">Subject type: {kind}.</span>}
    </div>
  )
}

function OutcomeCell({ request }) {
  if (request.convertedAt) {
    return (
      <div className="min-w-0">
        <p className="text-[#16181D]">Converted {formatDate(request.convertedAt) ?? '—'}</p>
        <p className="mt-0.5 text-xs text-[#6B7280]">A renewal task was created</p>
      </div>
    )
  }

  if (request.rejectedAt) {
    return (
      <div className="min-w-0">
        <p className="text-[#16181D]">Rejected {formatDate(request.rejectedAt) ?? '—'}</p>
        {request.rejectionReason ? (
          <p className="mt-0.5 break-words text-xs text-[#6B7280]">
            {requestDescriptionPreview(request.rejectionReason, 70)}
          </p>
        ) : null}
      </div>
    )
  }

  return <span className="text-[#9CA3AF]">Awaiting decision</span>
}

function DetailsButton({ request, onOpenDetails }) {
  return (
    <button
      type="button"
      onClick={() => onOpenDetails(request.id)}
      aria-label={`View details for the ${serviceRequestTypeText(request.type) ?? 'service'} request from ${formatDate(request.createdAt) ?? 'an unknown date'}`}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Eye size={13} strokeWidth={1.75} aria-hidden="true" />
      Details
    </button>
  )
}

function AdminServiceRequestTable({ items, resolveCompany, resolveEmployee, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[860px] border-collapse text-left">
          <caption className="sr-only">
            Service requests, newest first. Each row shows the request type, status,
            subject and company, submission date and outcome, with a Details action
            that opens the full request.
          </caption>

          <thead>
            <tr className="border-b border-[#E2E4E9]">
              <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Request
              </th>
              <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Status
              </th>
              <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Subject &amp; company
              </th>
              <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Submitted
              </th>
              <th scope="col" className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Outcome
              </th>
              <th scope="col" className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>

          <tbody>
            {items.map((request) => {
              const description = requestDescriptionPreview(request.description, 80)
              const typeLabel = serviceRequestTypeText(request.type) ?? 'Service request'

              return (
                <tr
                  key={request.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-gray-50/60"
                >
                  <td className="px-3 py-3 align-top">
                    <p className="text-[#16181D]">{typeLabel}</p>
                    {description ? (
                      <p className="mt-0.5 max-w-[26ch] break-words text-xs text-[#6B7280]">{description}</p>
                    ) : (
                      <p className="mt-0.5 text-xs text-[#9CA3AF]">No notes</p>
                    )}
                    <p className="mt-1 break-all font-mono text-[11px] text-[#9CA3AF]">{request.id}</p>
                  </td>

                  <td className="px-3 py-3 align-top">
                    <StatusPill
                      label={serviceRequestStatusText(request.status) ?? 'Unknown'}
                      tone={serviceRequestStatusTone(request.status)}
                    />
                  </td>

                  <td className="px-3 py-3 align-top">
                    <SubjectCell
                      request={request}
                      resolveCompany={resolveCompany}
                      resolveEmployee={resolveEmployee}
                    />
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {formatDate(request.createdAt) ?? <span className="text-[#9CA3AF]">&mdash;</span>}
                  </td>

                  <td className="px-3 py-3 align-top">
                    <OutcomeCell request={request} />
                  </td>

                  <td className="px-3 py-3 text-right align-top">
                    <DetailsButton request={request} onOpenDetails={onOpenDetails} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((request) => {
          const description = requestDescriptionPreview(request.description, 110)
          const typeLabel = serviceRequestTypeText(request.type) ?? 'Service request'

          return (
            <li
              key={request.id}
              className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="min-w-0 break-words text-sm font-semibold text-[#16181D]">{typeLabel}</p>
                <StatusPill
                  label={serviceRequestStatusText(request.status) ?? 'Unknown'}
                  tone={serviceRequestStatusTone(request.status)}
                />
              </div>

              <p className="mt-1 break-words text-xs text-[#6B7280]">
                {subjectKindText(request) ?? 'No subject'}
                {' · '}
                {formatDate(request.createdAt) ?? 'Unknown date'}
              </p>

              <div className="mt-2">
                <SubjectCell
                  request={request}
                  resolveCompany={resolveCompany}
                  resolveEmployee={resolveEmployee}
                />
              </div>

              {description ? (
                <p className="mt-2 break-words text-xs text-[#6B7280]">{description}</p>
              ) : null}

              <div className="mt-2.5">
                <OutcomeCell request={request} />
              </div>

              <div className="mt-3 flex justify-end">
                <DetailsButton request={request} onOpenDetails={onOpenDetails} />
              </div>
            </li>
          )
        })}
      </ul>

      {/*
        Both renderings iterate the same items, so the two are never out of step.
        The <ul> is hidden from assistive tech as well as from view at `md` and up
        by `md:hidden`, and the table is `hidden` below `md`, so a screen reader
        hears the list once, not twice.
      */}
      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#9CA3AF]">
        <Users size={13} strokeWidth={1.75} className="mt-px shrink-0" aria-hidden="true" />
        <span>
          Companies and employee names are resolved from Admin lookups. An entity
          reference is not resolved in this list, because the Admin API has no
          company-independent entity lookup; the request detail resolves it.
        </span>
      </p>
    </>
  )
}

export default AdminServiceRequestTable
