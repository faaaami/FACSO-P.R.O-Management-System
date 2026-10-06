import { Link } from 'react-router-dom'
import { ArrowRight, Building2 } from 'lucide-react'
import AdminDetailRow from '../clients/AdminDetailRow'
import { useAdminClient } from '../../../hooks/admin/useAdminClients'
import { displayText, formatDate, formatDateTime } from '../clients/clientDisplay'
import {
  employeeName,
  employeeStatusLabel,
  employeeStatusTone,
  employeeTenure,
} from './employeeDisplay'
import StatusPill from '../../client/StatusPill'

/**
 * Employee identity, employment and company context.
 *
 * FIELDS ARE EXACTLY THOSE ON GetEmployeeByIdResponseDto: id, clientEntityId,
 * clientCompanyId, entityName, fullName, passportNumber, dateOfBirth,
 * nationality, jobTitle, hireDate, isActive, isDeleted, createdAt, updatedAt.
 * Nothing else is shown. In particular there is NO position field, NO primary
 * flag, NO department and NO document count, because the DTO carries none of
 * them.
 *
 * THE COMPANY NAME COSTS A SECOND REQUEST, and the shape of that problem is worth
 * recording. The detail DTO carries `clientCompanyId` but no company name, while
 * the list DTO carries `entityName` but no company id at all. So the only way to
 * label an employee with their company is to resolve the id through
 * GET /admin/clients/{clientCompanyId}. That is a real endpoint and it is already
 * wrapped by useAdminClient under the key ['admin', 'client', …], which means it
 * is usually a cache hit for anyone who arrived from the Admin Clients drawer.
 *
 * The back-link therefore goes to `/clients?search=<companyName>`, NOT to a
 * clientId route. The approved Admin Clients page reads ?search= and seeds its
 * list from it; there is no ?clientId= contract, and adding one would mean
 * modifying the completed Phase 2 files, which is out of scope. Linking by name
 * is a slight loss of precision — a company whose name is a substring of another
 * may show more than one row — but it lands on a real, filtered result rather
 * than an invented route.
 *
 * If the company cannot be resolved, NO LINK IS RENDERED. Showing a dead or
 * guessed destination would be worse than showing the legal entity name alone,
 * which is the context that is actually on the record.
 *
 * `Tenure` is the one derived value here and it is labelled as derived, because
 * no DTO field reports it.
 */
function AdminEmployeeProfileSection({ employee }) {
  const companyId = employee?.clientCompanyId

  const {
    data: company,
    isLoading: companyLoading,
    isError: companyError,
    refresh: refreshCompany,
  } = useAdminClient(companyId)

  const companyName = company?.companyName
  const tenure = employeeTenure(employee?.hireDate)

  function renderCompanyRow() {
    if (!companyId) {
      return <AdminDetailRow label="Company" value={null} />
    }

    if (companyLoading) {
      return <AdminDetailRow label="Company" value="Resolving company…" />
    }

    if (companyError) {
      return (
        <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
          <dt className="text-xs font-medium text-[#6B7280]">Company</dt>
          <dd className="min-w-0 break-words text-sm text-[#16181D]">
            {/* Deliberately not a link: the name is unknown, so any destination
                would be a guess. A retry is offered instead, because this row is
                a failed request rather than a genuinely absent value. */}
            <span className="text-[#9CA3AF]">Unavailable</span>
            <button
              type="button"
              onClick={() => refreshCompany()}
              className="ml-2 rounded-[6px] text-xs font-semibold text-[#0F9D74] underline decoration-transparent transition duration-150 hover:decoration-[rgba(15,157,116,0.45)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
            >
              Retry
              <span className="sr-only"> loading the company record</span>
            </button>
          </dd>
        </div>
      )
    }

    if (!companyName) {
      return <AdminDetailRow label="Company" value={null} />
    }

    return (
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
            <span className="sr-only"> — open in the Admin Clients list</span>
          </Link>
        </dd>
      </div>
    )
  }

  return (
    <div className="flex flex-col gap-6">
      <section>
        <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
          Identity
        </h3>
        <dl>
          <AdminDetailRow label="Full name" value={employeeName(employee)} />
          <AdminDetailRow
            label="Passport number"
            value={displayText(employee?.passportNumber)}
            mono
          />
          <AdminDetailRow
            label="Date of birth"
            value={formatDate(employee?.dateOfBirth)}
          />
          <AdminDetailRow
            label="Nationality"
            value={displayText(employee?.nationality)}
          />
        </dl>
      </section>

      <section>
        <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
          Employment
        </h3>
        <dl>
          <AdminDetailRow
            label="Job title"
            value={displayText(employee?.jobTitle)}
          />
          <AdminDetailRow
            label="Hire date"
            value={formatDate(employee?.hireDate)}
          />
          <AdminDetailRow
            label="Tenure"
            value={
              tenure ? (
                <span>
                  {tenure}
                  <span className="ml-1.5 text-xs text-[#6B7280]">(derived)</span>
                </span>
              ) : null
            }
          />
          <AdminDetailRow
            label="Status"
            value={
              <StatusPill
                label={employeeStatusLabel(employee)}
                tone={employeeStatusTone(employee)}
              />
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
          {renderCompanyRow()}
        </dl>
      </section>

      <section>
        <h3 className="mb-1 text-sm font-semibold tracking-tight text-[#16181D]">
          Record
        </h3>
        <dl>
          <AdminDetailRow
            label="Created"
            value={formatDateTime(employee?.createdAt)}
          />
          <AdminDetailRow
            label="Last updated"
            value={formatDateTime(employee?.updatedAt)}
          />
        </dl>
      </section>
    </div>
  )
}

export default AdminEmployeeProfileSection
