import { Building2, Mail, MapPin, Phone } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import { MISSING_VALUE, presentText, recordStatusLabel, recordStatusTone, truncate } from './clientDisplay'

/**
 * Admin client-company list, as a table on wide screens and a card stack below
 * `md` so a 390px viewport never needs horizontal scrolling to read a row.
 *
 * COLUMN CHOICE IS CONSTRAINED BY THE DTO, not by taste. The list item carries
 * no address and no updatedAt, so neither appears here — those live only on the
 * detail DTO and belong in the drawer. Adding an Address column would render a
 * column of em dashes.
 *
 * Long company names are truncated in the cell with the full value kept in
 * `title`, so nothing is lost on hover or to a screen reader, and the cell
 * wraps rather than forcing the table wider than the viewport.
 *
 * The Details button carries an sr-only company name so the action is
 * distinguishable when several identical "Details" labels are on screen.
 */
function AdminClientsTable({ items, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">Client companies visible to Admin</caption>
          <thead>
            <tr className="border-b border-[#E2E4E9]">
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] first:pl-0 last:pr-0"
              >
                Company
              </th>
              <th
                scope="col"
                className="hidden px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] lg:table-cell"
              >
                Trade licence
              </th>
              <th
                scope="col"
                className="hidden px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] lg:table-cell"
              >
                Contact
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Emirate
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Status
              </th>
              <th scope="col" className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280] last:pr-0">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((company) => {
              const name = presentText(company?.companyName) ?? 'Unnamed company'
              const licence = presentText(company?.tradeLicenseNumber)
              const email = presentText(company?.email)
              const phone = presentText(company?.phone)
              const emirate = presentText(company?.emirate)

              return (
                <tr key={company?.id} className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-[#F7F8FA]/60">
                  <td className="max-w-[22rem] px-3 py-3.5 align-middle first:pl-0">
                    <div className="flex items-center gap-2">
                      <Building2 size={15} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
                      <span className="min-w-0 break-words font-semibold text-[#16181D]" title={name}>
                        {truncate(name, 52) ?? MISSING_VALUE}
                      </span>
                    </div>
                  </td>
                  <td className="hidden px-3 py-3.5 align-middle text-[#16181D] lg:table-cell">
                    {licence ? <span className="font-mono text-[13px]">{licence}</span> : <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>}
                  </td>
                  <td className="hidden max-w-[16rem] px-3 py-3.5 align-middle lg:table-cell">
                    {email || phone ? (
                      <div className="flex min-w-0 flex-col gap-0.5">
                        {email && (
                          <span className="flex min-w-0 items-center gap-1.5">
                            <Mail size={12} className="shrink-0 text-[#9CA3AF]" aria-hidden="true" />
                            <span className="truncate text-[#16181D]" title={email}>
                              {email}
                            </span>
                          </span>
                        )}
                        {phone && (
                          <span className="flex min-w-0 items-center gap-1.5">
                            <Phone size={12} className="shrink-0 text-[#9CA3AF]" aria-hidden="true" />
                            <span className="truncate text-[#16181D]">{phone}</span>
                          </span>
                        )}
                      </div>
                    ) : (
                      <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>
                    )}
                  </td>
                  <td className="px-3 py-3.5 align-middle text-[#16181D]">
                    {emirate ? (
                      <span className="inline-flex items-center gap-1">
                        <MapPin size={12} className="shrink-0 text-[#9CA3AF]" aria-hidden="true" />
                        {emirate}
                      </span>
                    ) : (
                      <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>
                    )}
                  </td>
                  <td className="px-3 py-3.5 align-middle">
                    <StatusPill label={recordStatusLabel(company)} tone={recordStatusTone(company)} />
                  </td>
                  <td className="px-3 py-3.5 text-right align-middle last:pr-0">
                    <button
                      type="button"
                      onClick={() => onOpenDetails(company?.id)}
                      className="rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
                    >
                      Details
                      <span className="sr-only"> for {name}</span>
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((company) => {
          const name = presentText(company?.companyName) ?? 'Unnamed company'
          const licence = presentText(company?.tradeLicenseNumber)
          const email = presentText(company?.email)
          const phone = presentText(company?.phone)
          const emirate = presentText(company?.emirate)

          return (
            <li
              key={company?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2">
                  <Building2 size={15} className="shrink-0 text-[#6B7280]" aria-hidden="true" />
                  <span className="min-w-0 break-words text-sm font-semibold text-[#16181D]">{name}</span>
                </div>
                <StatusPill label={recordStatusLabel(company)} tone={recordStatusTone(company)} />
              </div>

              <dl className="mt-3 flex flex-col gap-1.5">
                {licence && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="text-xs text-[#6B7280]">Trade licence</dt>
                    <dd className="min-w-0 break-all text-right font-mono text-[13px] text-[#16181D]">{licence}</dd>
                  </div>
                )}
                {emirate && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="text-xs text-[#6B7280]">Emirate</dt>
                    <dd className="text-right text-xs font-medium text-[#16181D]">{emirate}</dd>
                  </div>
                )}
                {email && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="text-xs text-[#6B7280]">Email</dt>
                    <dd className="min-w-0 break-all text-right text-xs font-medium text-[#16181D]">{email}</dd>
                  </div>
                )}
                {phone && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="text-xs text-[#6B7280]">Phone</dt>
                    <dd className="text-right text-xs font-medium text-[#16181D]">{phone}</dd>
                  </div>
                )}
              </dl>

              <button
                type="button"
                onClick={() => onOpenDetails(company?.id)}
                className="mt-3 w-full rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-white focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
              >
                Details
                <span className="sr-only"> for {name}</span>
              </button>
            </li>
          )
        })}
      </ul>
    </>
  )
}

export default AdminClientsTable
