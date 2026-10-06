import { Building2, ExternalLink, FileText, User } from 'lucide-react'
import AdminDetailRow from '../clients/AdminDetailRow'
import { documentTypeLabel } from '../documents/documentDisplay'
import { formatDate, shortGuid, subjectKindText } from './serviceRequestDisplay'

/**
 * What the request is about: the client company, the subject, and — only for an
 * EarlyRenewal — the document it was resolved against.
 *
 * THE DTO CARRIES NO NAMES. ServiceRequestListItemDto and
 * GetServiceRequestResponseDto both hold bare Guids for clientCompanyId,
 * employeeId?, entityId? and documentId?, and no Admin route joins them to names.
 * So each value below is presented with its own honest resolution state, and an
 * unresolved id is labelled as unresolved rather than dressed up as a name.
 *
 * COMPANY IS THE ONE LINK. The client detail page already reads /clients?search=,
 * so a resolved company links there. There is deliberately no employee or entity
 * link: /clients?employee=<id> and /clients?entity=<id> are not search
 * parameters that page reads, and the Admin Employees/Entities routes are
 * company- or agent-scoped with no id-only form. A link that silently changes the
 * destination's meaning is worse than text, so those values stay text.
 *
 * THE ENTITY RESOLVES ONLY IN THE DRAWER. There is no /admin/entities/{id} route;
 * the only entity-by-id Admin route needs a company id as well. The drawer has
 * its own record's company and so resolves its entity from a company-scoped page.
 * A list row has no single company to pair with an entity id, so resolving there
 * would mean walking every company — which is why this section never asks the
 * list to do it, and why an entity name appears here but not in the table.
 *
 * documentId IS A REFERENCE, NOT AN ATTACHMENT. It is set only for EarlyRenewal,
 * and it points at a document that already exists in the registry; the request did
 * not upload anything. So it is shown as the document's own identity — number,
 * type, expiry — and never as a file link: the fileUrl on that document belongs
 * to the Documents module, and presenting it here would imply the request owns an
 * attachment it never had.
 */
function AdminServiceRequestSubjectSection({ request, subjects }) {
  const company = subjects.resolveCompany(request.clientCompanyId)
  const employee = subjects.resolveEmployee(request.employeeId)
  const entity = subjects.resolveEntity(request.entityId)
  const document = subjects.resolveDocument()
  const kind = subjectKindText(request)

  const companyName = company?.resolved ? company.name : null
  const companyLabel = companyName ?? `Unresolved company · ${shortGuid(request.clientCompanyId)}`

  return (
    <div className="flex flex-col gap-5">
      <section
        aria-labelledby="admin-service-request-company-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-service-request-company-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <Building2 size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          Client company
        </h3>

        <div className="mt-3">
          {companyName ? (
            <a
              href={`/clients?search=${encodeURIComponent(companyName)}`}
              className="inline-flex cursor-pointer items-center gap-1.5 break-words text-sm font-medium text-[#0F9D74] underline decoration-[rgba(15,157,116,0.35)] underline-offset-2 transition hover:decoration-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              {companyName}
              <ExternalLink size={12} strokeWidth={1.75} aria-hidden="true" />
            </a>
          ) : (
            <p className="break-words text-sm text-[#9CA3AF]" title={String(request.clientCompanyId)}>
              {companyLabel}
            </p>
          )}
        </div>

        <dl className="mt-2">
          <AdminDetailRow label="Company ID" value={request.clientCompanyId} mono />
        </dl>

        {!companyName && (
          <p className="mt-1.5 text-xs text-[#9CA3AF]">
            The company name is looked up from the shared Admin client map. An id
            appears here when the company is outside that map, so this request
            cannot be linked to a client page.
          </p>
        )}
      </section>

      <section
        aria-labelledby="admin-service-request-subject-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-service-request-subject-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <User size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          Request subject
          {kind ? <span className="text-xs font-normal text-[#6B7280]">&middot; {kind}</span> : null}
        </h3>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          A request is about an employee, a legal entity, or a specific employee
          belonging to a specific entity. All three references are shown below when
          the record carries them, because a request may hold an employee and that
          employee&rsquo;s entity at the same time.
        </p>

        <dl className="mt-3">
          <AdminDetailRow
            label="Employee"
            value={
              employee.kind === 'none'
                ? null
                : employee.resolved
                  ? employee.name
                  : `Unresolved employee · ${shortGuid(employee.id)}`
            }
          />
          {employee.kind === 'employee' ? (
            <AdminDetailRow label="Employee ID" value={employee.id} mono />
          ) : null}
          {employee.entityName ? (
            <AdminDetailRow label="Employee's entity" value={employee.entityName} />
          ) : null}

          <AdminDetailRow
            label="Entity"
            value={
              entity.kind === 'none'
                ? null
                : entity.resolved
                  ? entity.name
                  : `Unresolved entity · ${shortGuid(entity.id)}`
            }
          />
          {entity.kind === 'entity' ? (
            <AdminDetailRow label="Entity ID" value={entity.id} mono />
          ) : null}
        </dl>

        {!employee.resolved && employee.kind === 'employee' && (
          <p className="mt-1.5 text-xs text-[#9CA3AF]">
            Employee names come from the shared Admin employee lookup, which is
            capped at 1000 rows. An employee beyond that cap is shown by id only.
          </p>
        )}

        {!entity.resolved && entity.kind === 'entity' && (
          <p className="mt-1.5 text-xs text-[#9CA3AF]">
            The entity name is looked up from this company&rsquo;s entities. If it is
            unresolved, the entity is not in this company&rsquo;s list — for example
            it may have been moved or removed since the request was submitted.
          </p>
        )}
      </section>

      <section
        aria-labelledby="admin-service-request-document-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-service-request-document-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <FileText size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          Linked document
        </h3>

        <dl className="mt-3">
          <AdminDetailRow
            label="Document number"
            value={document.kind === 'document' ? document.number : null}
          />
          <AdminDetailRow
            label="Document type"
            value={document.kind === 'document' && document.type ? documentTypeLabel(document.type) : null}
          />
          <AdminDetailRow
            label="Document expiry"
            value={document.kind === 'document' ? formatDate(document.expiryDate) : null}
          />
          <AdminDetailRow
            label="Document ID"
            value={document.kind === 'document' ? document.id : null}
            mono
          />
        </dl>

        {document.kind !== 'document' ? (
          <p className="mt-2 text-xs leading-relaxed text-[#9CA3AF]">
            No document is linked to this request. A document reference is recorded
            only for an <span className="font-medium text-[#6B7280]">Early renewal</span>{' '}
            request, where the client asked for a renewal ahead of a visa expiry and
            named the document it refers to. A new visa or other request is not
            resolved against a document, so there is nothing to show here.
          </p>
        ) : !document.resolved ? (
          <p className="mt-2 text-xs leading-relaxed text-[#9CA3AF]">
            The document id is recorded, but the document itself could not be read.
            It may have been deleted, or the lookup may have failed; the request keeps
            its reference either way.
          </p>
        ) : (
          <p className="mt-2 text-xs leading-relaxed text-[#9CA3AF]">
            This is a reference to an existing registry document, not a file attached
            to the request. The request uploaded nothing.
          </p>
        )}
      </section>
    </div>
  )
}

export default AdminServiceRequestSubjectSection
