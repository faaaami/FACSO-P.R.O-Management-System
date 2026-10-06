import { Link } from 'react-router-dom'
import { ArrowRight, Building2, UserRound } from 'lucide-react'
import AdminClientSection from '../clients/AdminClientSection'
import AdminDetailRow from '../clients/AdminDetailRow'
import { useAdminEmployee } from '../../../hooks/admin/useAdminEmployees'
import { useAdminClient } from '../../../hooks/admin/useAdminClients'
import { displayText, formatDate } from '../clients/clientDisplay'
import { shortGuid } from './documentDisplay'

/**
 * Who a document belongs to, resolved as far as the Admin API actually allows.
 *
 * THE REGISTRY ROW CARRIES OWNER IDS AND NOTHING ELSE. ExpiringDocumentListItemDto
 * has clientEntityId? and employeeId? — no names, and no clientCompanyId either, so
 * an owner label cannot come from the row and has to be resolved. Two very different
 * situations result, and the difference is a backend limit rather than a missing
 * lookup.
 *
 * EMPLOYEE-OWNED: FULLY RESOLVED, IN TWO STEPS.
 *   Step 1. GET /admin/employees/{employeeId} returns fullName, entityName AND
 *           clientCompanyId. This is the only DTO anywhere that carries a company id
 *           for a person, which is why the employee detail is fetched rather than
 *           reusing the cheap name map the table uses.
 *   Step 2. That clientCompanyId is resolved to a company NAME through
 *           GET /admin/clients/{clientCompanyId} (useAdminClient), which is already
 *           cached under ['admin', 'client', …] for anyone who has opened a company
 *           drawer.
 *   Both hooks are called unconditionally on every render; each one disables itself
 *   internally when handed a null id, so an entity-only document simply never
 *   triggers a request.
 *
 * ENTITY-OWNED: NOT RESOLVABLE, AND SAY SO.
 *   There is no /api/v1/admin/entities/{entityId} route, and the one entity-by-id
 *   route that exists — /admin/clients/{clientId}/entities/{entityId} — is scoped
 *   by BOTH ids. A registry row has no company id, so that route cannot be called
 *   from here, and the company-scoped repository method behind it is wired only to
 *   the Client module. There is therefore no safe lookup for this owner, and none is
 *   invented: the row shows a truncated GUID, labelled as unresolved, with the full
 *   value in the tooltip. No entity or company name is guessed, and no client link
 *   is offered, because any destination would be a fabrication.
 *
 * THE CLIENT BACK-LINK GOES BY NAME, NOT BY ID.
 * `/clients?search=<companyName>` is the only contract the completed Admin Clients
 * page actually reads; there is no ?clientId= parameter, and adding one would mean
 * modifying finished Phase 2 files, which is out of scope. Linking by name can match
 * more than one company when one name is a substring of another, but it lands on a
 * real filtered result rather than an invented route. If the company cannot be
 * resolved, NO LINK IS RENDERED — a dead or guessed destination is worse than
 * showing the legal entity name, which is context that genuinely exists.
 *
 * There is no link to the employee either: the Admin Employees page has no
 * employeeId deep link, so it is not given an invented one.
 */
function AdminDocumentOwnerSection({ document }) {
  const employeeId = document?.employeeId ?? null

  const {
    data: employee,
    isLoading: employeeLoading,
    isError: employeeError,
    refresh: refreshEmployee,
  } = useAdminEmployee(employeeId)

  // Only the employee DETAIL carries a company id, so this is null until step 1
  // resolves. useAdminClient disables itself for a null id.
  const companyId = employee?.clientCompanyId ?? null

  const {
    data: company,
    isLoading: companyLoading,
    isError: companyError,
    refresh: refreshCompany,
  } = useAdminClient(companyId)

  const companyName = company?.companyName
  const entityId = document?.clientEntityId ?? null

  function renderEntityOnly() {
    const short = shortGuid(entityId)

    return (
      <div className="flex flex-col gap-3">
        <div className="flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 p-3.5">
          <Building2
            size={15}
            strokeWidth={1.75}
            className="mt-0.5 shrink-0 text-[#92400E]"
            aria-hidden="true"
          />
          <div className="min-w-0">
            <p className="text-xs font-semibold text-[#92400E]">
              Owner not resolved
            </p>
            <p className="mt-1 break-words text-xs text-[#92400E]">
              This document belongs to a legal entity rather than to an employee.
              The Admin API cannot resolve an entity from this record: there is no
              entity-by-id route that does not also require a company id, and the
              registry row does not carry one.
            </p>
          </div>
        </div>

        <dl>
          <AdminDetailRow
            label="Legal entity ID"
            value={short ?? displayText(null)}
            mono
          />
        </dl>

        {entityId && (
          <p className="text-xs text-[#6B7280]">
            Full identifier:{' '}
            <span className="break-all font-mono text-[#16181D]">{entityId}</span>
          </p>
        )}
      </div>
    )
  }

  function renderEmployee() {
    if (employeeLoading) {
      return (
        <p className="text-xs text-[#6B7280]" role="status">
          Resolving employee…
        </p>
      )
    }

    if (employeeError) {
      return (
        <div className="flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 p-3.5">
          <UserRound
            size={15}
            strokeWidth={1.75}
            className="mt-0.5 shrink-0 text-[#92400E]"
            aria-hidden="true"
          />
          <div className="min-w-0">
            <p className="text-xs font-semibold text-[#92400E]">
              Owner not resolved
            </p>
            <p className="mt-1 break-words text-xs text-[#92400E]">
              The employee record for this document could not be loaded, so no name
              is shown rather than a guessed one.
            </p>
            <button
              type="button"
              onClick={() => refreshEmployee()}
              className="mt-2 cursor-pointer rounded-[6px] text-xs font-semibold text-[#0F9D74] underline decoration-transparent transition duration-150 hover:decoration-[rgba(15,157,116,0.45)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
            >
              Retry
              <span className="sr-only"> loading the employee record</span>
            </button>
          </div>
        </div>
      )
    }

    return (
      <div className="flex flex-col gap-6">
        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            Employee
          </h3>
          <dl>
            <AdminDetailRow
              label="Full name"
              value={displayText(employee?.fullName)}
            />
            <AdminDetailRow
              label="Job title"
              value={displayText(employee?.jobTitle)}
            />
            <AdminDetailRow
              label="Hire date"
              value={formatDate(employee?.hireDate)}
            />
            <AdminDetailRow
              label="Employee record"
              value={
                employee?.isDeleted ? 'Deleted' : employee?.isActive ? 'Active' : 'Inactive'
              }
            />
          </dl>
        </section>

        <section>
          <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
            Company context
          </h3>
          <dl>
            <AdminDetailRow
              label="Legal entity"
              value={displayText(employee?.entityName)}
            />

            {/*
              The company row has four honest outcomes and no fifth: no company id
              yet (still resolving), a failed lookup (error plus retry), a successful
              lookup with a name (a link), and a successful lookup without a name
              (a genuine absence, shown as the dash). A dead link is never rendered.
            */}
            {companyLoading && (
              <AdminDetailRow label="Company" value="Resolving company…" />
            )}

            {companyError && (
              <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
                <dt className="text-xs font-medium text-[#6B7280]">Company</dt>
                <dd className="min-w-0 break-words text-sm text-[#16181D]">
                  <span className="text-[#9CA3AF]">Unavailable</span>
                  <button
                    type="button"
                    onClick={() => refreshCompany()}
                    className="ml-2 cursor-pointer rounded-[6px] text-xs font-semibold text-[#0F9D74] underline decoration-transparent transition duration-150 hover:decoration-[rgba(15,157,116,0.45)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                  >
                    Retry
                    <span className="sr-only"> loading the company record</span>
                  </button>
                </dd>
              </div>
            )}

            {!companyLoading && !companyError && companyName && (
              <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
                <dt className="text-xs font-medium text-[#6B7280]">Company</dt>
                <dd className="min-w-0 break-words text-sm text-[#16181D]">
                  <Link
                    to={`/clients?search=${encodeURIComponent(companyName)}`}
                    className="inline-flex items-center gap-1.5 rounded-[6px] font-medium text-[#0F9D74] underline decoration-transparent transition duration-150 hover:decoration-[rgba(15,157,116,0.45)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                  >
                    <Building2 size={13} strokeWidth={1.75} aria-hidden="true" />
                    {companyName}
                    <ArrowRight size={13} strokeWidth={2} aria-hidden="true" />
                    <span className="sr-only">
                      {' '}
                      — open in the Admin Clients list
                    </span>
                  </Link>
                </dd>
              </div>
            )}

            {!companyLoading && !companyError && !companyName && (
              <AdminDetailRow label="Company" value={null} />
            )}
          </dl>
        </section>
      </div>
    )
  }

  function renderOwner() {
    if (employeeId) {
      return renderEmployee()
    }
    if (entityId) {
      return renderEntityOnly()
    }
    return (
      <p className="text-xs text-[#6B7280]">
        This document is not linked to a client entity or an employee, so it has no
        owner to show.
      </p>
    )
  }

  return (
    <AdminClientSection
      title="Owner"
      description="Resolved from the document's owner reference where the Admin API allows it."
    >
      {renderOwner()}
    </AdminClientSection>
  )
}

export default AdminDocumentOwnerSection
