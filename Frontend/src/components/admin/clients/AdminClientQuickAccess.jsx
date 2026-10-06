import { Link } from 'react-router-dom'
import { ArrowRight, FileText, Receipt, Users } from 'lucide-react'
import AdminClientSection from './AdminClientSection'
import { displayText, presentText } from './clientDisplay'

/**
 * Outbound navigation from the client drawer.
 *
 * EVERY LINK HERE IS DELIBERATELY UNFILTERED OR EXPLICITLY CAVEATED, because a
 * deep link whose filter the destination ignores is worse than no filter — it
 * looks like it worked.
 *
 *  - Employees: /employees?clientId=<id>. The employees page is a placeholder
 *    that does not read URL params yet, so the param is correct and expected but
 *    not yet applied. The embedded section in this drawer is the reliable view.
 *  - Invoices: PLAIN /invoices. All four invoice resources do support
 *    clientCompanyId server-side, so ?clientCompanyId=<id> would be honoured by
 *    the API — but the Admin Invoices page is also a placeholder and would drop
 *    the filter. Shipping a link that visibly does nothing is worse than
 *    shipping an honest unfiltered one, so the param is held back until the
 *    destination can consume it.
 *  - Documents: PLAIN /documents. The Admin Documents module is the expiry and
 *    renewal registry. There is no global admin document list and it cannot be
 *    filtered by client, so it is linked as a module rather than presented as
 *    "this company's documents", which the API cannot answer.
 *
 * NOT LINKED, ON PURPOSE: service requests. GET /service-requests accepts only
 * page and pageSize — there is no client filter at all — so there is nothing to
 * pass and no honest client-scoped view to offer. Renewal tasks are left out for
 * the same reason: the clientCompanyId filter is real, but the destination does
 * not read it, and it has no place in a section that also has no server-side
 * search to make it navigable.
 */
function AdminClientQuickAccess({ clientId, companyName }) {
  const links = [
    {
      to: `/employees?clientId=${encodeURIComponent(clientId)}`,
      icon: Users,
      label: 'Employees',
      description: 'Open the Employees module scoped to this company.',
    },
    {
      to: '/invoices',
      icon: Receipt,
      label: 'Invoices',
      description: 'Open the Invoices module. Client filtering is not applied there yet.',
    },
    {
      to: '/documents',
      icon: FileText,
      label: 'Expiry & renewal registry',
      description: 'Open the document registry. It cannot be filtered to this company.',
    },
  ]

  return (
    <AdminClientSection
      title="Quick access"
      description="Related modules for this company."
    >
      <ul className="flex flex-col gap-2.5">
        {links.map((link) => {
          const Icon = link.icon
          return (
            <li key={link.to}>
              <Link
                to={link.to}
                className="flex items-start gap-3 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5 transition duration-150 hover:border-[#0F9D74]/30 hover:bg-[rgba(15,157,116,0.04)] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-white text-[#16181D]">
                  <Icon size={16} strokeWidth={1.75} aria-hidden="true" />
                </span>
                <span className="min-w-0 flex-1">
                  <span className="flex items-center gap-1.5 text-sm font-semibold text-[#16181D]">
                    {link.label}
                    <ArrowRight size={13} strokeWidth={2} className="shrink-0 text-[#0F9D74]" aria-hidden="true" />
                  </span>
                  <span className="mt-0.5 block text-xs text-[#6B7280]">
                    {link.description}
                  </span>
                </span>
                {companyName && <span className="sr-only"> for {displayText(companyName)}</span>}
              </Link>
            </li>
          )
        })}
      </ul>

      {presentText(companyName) && (
        <p className="mt-4 text-xs text-[#6B7280]">
          Links open the module for {companyName}. Where a description says a filter
          is not applied, the destination module does not yet read it.
        </p>
      )}
    </AdminClientSection>
  )
}

export default AdminClientQuickAccess
