import { UserRound } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  MISSING_VALUE,
  displayText,
  formatDate,
  presentText,
  truncate,
} from '../clients/clientDisplay'
import { employeeName, employeeStatusLabel, employeeStatusTone } from './employeeDisplay'

/**
 * Admin employee list, as a table on wide screens and a card stack below `md` so
 * a 390px viewport never needs horizontal scrolling to read a row.
 *
 * COLUMN CHOICE IS CONSTRAINED BY THE LIST DTO, not by taste.
 * GetEmployeeListItemDto carries no clientCompanyId, so there is deliberately NO
 * company column: the only company-adjacent context a row can honestly show is
 * `entityName`, which the backend supplies. A "Company" column would have to
 * invent one, and linking a row to its company is not possible from the list
 * payload at all. The company becomes reachable only in the drawer, where the
 * detail DTO adds clientCompanyId.
 *
 * The list DTO also carries no dateOfBirth and no updatedAt, so neither appears
 * here — both are detail-only fields and belong in the drawer.
 *
 * There is no sort control and no sortable-column affordance because the endpoint
 * hardcodes `ORDER BY e.created_at DESC, e.id ASC`. The header deliberately shows
 * plain static labels; making a column look clickable would promise an ordering
 * the API cannot produce.
 *
 * The Details button carries an sr-only employee name so the action stays
 * distinguishable when several identical "Details" labels are on screen.
 */
function AdminEmployeesTable({ items, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">
            Employees visible to Admin, newest records first
          </caption>
          <thead>
            <tr className="border-b border-[#E2E4E9]">
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] first:pl-0"
              >
                Employee
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Legal entity
              </th>
              <th
                scope="col"
                className="hidden px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] lg:table-cell"
              >
                Job title
              </th>
              <th
                scope="col"
                className="hidden px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] lg:table-cell"
              >
                Nationality
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Hire date
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Status
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280] last:pr-0"
              >
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((employee) => {
              const name = employeeName(employee)
              const entity = presentText(employee?.entityName)
              const jobTitle = presentText(employee?.jobTitle)
              const nationality = presentText(employee?.nationality)

              return (
                <tr
                  key={employee?.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-[#F7F8FA]/60"
                >
                  <td className="max-w-[20rem] px-3 py-3.5 align-middle first:pl-0">
                    <div className="flex items-center gap-2">
                      <UserRound
                        size={15}
                        className="shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <span
                        className="min-w-0 break-words font-semibold text-[#16181D]"
                        title={name}
                      >
                        {truncate(name, 52) ?? MISSING_VALUE}
                      </span>
                    </div>
                  </td>
                  <td className="max-w-[16rem] px-3 py-3.5 align-middle text-[#16181D]">
                    {entity ? (
                      <span className="min-w-0 break-words" title={entity}>
                        {truncate(entity, 40) ?? MISSING_VALUE}
                      </span>
                    ) : (
                      <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>
                    )}
                  </td>
                  <td className="hidden max-w-[14rem] px-3 py-3.5 align-middle lg:table-cell">
                    <span className="min-w-0 break-words text-[#16181D]">
                      {jobTitle ?? <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>}
                    </span>
                  </td>
                  <td className="hidden px-3 py-3.5 align-middle text-[#16181D] lg:table-cell">
                    {nationality ?? (
                      <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>
                    )}
                  </td>
                  <td className="px-3 py-3.5 align-middle whitespace-nowrap text-[#16181D]">
                    {formatDate(employee?.hireDate)}
                  </td>
                  <td className="px-3 py-3.5 align-middle">
                    <StatusPill
                      label={employeeStatusLabel(employee)}
                      tone={employeeStatusTone(employee)}
                    />
                  </td>
                  <td className="px-3 py-3.5 text-right align-middle last:pr-0">
                    <button
                      type="button"
                      onClick={() => onOpenDetails(employee?.id)}
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
        {items.map((employee) => {
          const name = employeeName(employee)
          const entity = presentText(employee?.entityName)
          const jobTitle = presentText(employee?.jobTitle)
          const nationality = presentText(employee?.nationality)

          return (
            <li
              key={employee?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2">
                  <UserRound
                    size={15}
                    className="shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <span className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                    {name}
                  </span>
                </div>
                <StatusPill
                  label={employeeStatusLabel(employee)}
                  tone={employeeStatusTone(employee)}
                />
              </div>

              <dl className="mt-3 flex flex-col gap-1.5">
                {entity && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="shrink-0 text-xs text-[#6B7280]">Legal entity</dt>
                    <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                      {entity}
                    </dd>
                  </div>
                )}
                {jobTitle && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="shrink-0 text-xs text-[#6B7280]">Job title</dt>
                    <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                      {jobTitle}
                    </dd>
                  </div>
                )}
                {nationality && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="shrink-0 text-xs text-[#6B7280]">Nationality</dt>
                    <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                      {nationality}
                    </dd>
                  </div>
                )}
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Hire date</dt>
                  <dd className="text-right text-xs font-medium text-[#16181D]">
                    {formatDate(employee?.hireDate)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="shrink-0 text-xs text-[#6B7280]">Passport</dt>
                  <dd className="min-w-0 break-all text-right font-mono text-[13px] text-[#16181D]">
                    {displayText(employee?.passportNumber)}
                  </dd>
                </div>
              </dl>

              <button
                type="button"
                onClick={() => onOpenDetails(employee?.id)}
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

export default AdminEmployeesTable
