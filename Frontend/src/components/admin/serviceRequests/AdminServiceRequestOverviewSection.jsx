import StatusPill from '../../client/StatusPill'
import AdminDetailRow from '../clients/AdminDetailRow'
import {
  formatDateTime,
  requestDescriptionText,
  serviceRequestStatusText,
  serviceRequestStatusTone,
  serviceRequestTypeText,
} from './serviceRequestDisplay'

/**
 * Overview of one service request, from GetServiceRequestResponseDto.
 *
 * The detail DTO is the list DTO plus updatedAt and nothing else, so this
 * section can show identity, type, status, the client's own notes and the two
 * timestamps — and deliberately no more. There is no assignee, no employee name,
 * no document number, no comment, no attachment and no invoice field on either
 * DTO, and none is inferred. Subject context lives in its own tab, and the
 * terminal outcome lives in the Lifecycle tab.
 *
 * THE RECORD IS NOT EDITABLE, AND THAT IS STATED. A reader who knows a service
 * request can be converted or rejected would otherwise assume the drawer is
 * missing those controls too. The note separates the two ideas precisely: the
 * request's own fields cannot be edited at all, and the one thing that can be
 * done to it — a decision — is taken from the controls at the foot of the drawer
 * while it is still submitted, and cannot be undone.
 *
 * `description` is the free-text notes the client typed at submission and is
 * frequently absent, so it is rendered as a paragraph when present and as the
 * honest missing dash when not — never as an empty block.
 */
function AdminServiceRequestOverviewSection({ request }) {
  const description = requestDescriptionText(request.description)

  return (
    <div className="flex flex-col gap-5">
      <section
        aria-labelledby="admin-service-request-overview-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-service-request-overview-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Request
        </h3>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <StatusPill
            label={serviceRequestStatusText(request.status) ?? 'Unknown'}
            tone={serviceRequestStatusTone(request.status)}
          />
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-medium text-[#6B7280]">
            {serviceRequestTypeText(request.type) ?? 'Service request'}
          </span>
        </div>

        <dl className="mt-3.5 grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-2">
          <AdminDetailRow label="Request ID" value={request.id} mono />
          <AdminDetailRow label="Type" value={serviceRequestTypeText(request.type)} />
          <AdminDetailRow label="Submitted" value={formatDateTime(request.createdAt)} />
          <AdminDetailRow label="Last updated" value={formatDateTime(request.updatedAt)} />
        </dl>
      </section>

      <section
        aria-labelledby="admin-service-request-notes-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-service-request-notes-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Notes from the client
        </h3>

        {description ? (
          <p className="mt-2.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
            {description}
          </p>
        ) : (
          <p className="mt-2.5 text-sm text-[#9CA3AF]">
            The client submitted this request without any notes.
          </p>
        )}

        <p className="mt-3 text-xs text-[#9CA3AF]">
          Notes are free text typed at submission. They are stored verbatim and are
          not validated against any document or entity.
        </p>
      </section>

      <section
        aria-labelledby="admin-service-request-readonly-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
      >
        <h3
          id="admin-service-request-readonly-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          What can still be done with this request
        </h3>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          The request itself is not editable. Its type, subject, notes and status
          cannot be changed by any endpoint — there is no update route — so nothing
          here will become editable by reloading.
        </p>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          What can be done is a decision, and only while the request is still
          submitted: convert it into a renewal task, or reject it with a reason.
          Both transitions are one-way. There is no endpoint that returns a
          converted or rejected request to submitted, and no way to reverse either,
          so a decision cannot be undone once it has been taken. Conversion also
          creates a renewal task in a separate module.
        </p>
      </section>
    </div>
  )
}

export default AdminServiceRequestOverviewSection
