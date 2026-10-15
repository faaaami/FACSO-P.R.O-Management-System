import { FileClock } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  MISSING_VALUE,
  displayText,
  formatDate,
  presentText,
  truncate,
} from '../clients/clientDisplay'
import {
  daysRemainingText,
  registryStatusLabel,
  registryStatusTone,
} from '../documents/documentDisplay'

/**
 * Employee documents that are expiring, as a table on wide screens and a card
 * stack below `md` so a 390px viewport never needs horizontal scrolling.
 *
 * ONE ROW PER DOCUMENT, NOT PER EMPLOYEE. An employee with two expiring documents
 * appears twice, which is correct here: each row is a thing that will lapse. The
 * employee name is not de-duplicated or grouped, and no per-employee subtotal is
 * shown, because a row count here is a count of documents.
 *
 * NO LEGAL ENTITY COLUMN, DELIBERATELY. The DTO carries `clientEntityId` but no
 * entity name, and useAdminEntityMaps resolves client COMPANIES and staff only —
 * there is no legal-entity map anywhere in the Admin API. Passing an entity id
 * into resolveClient would be a category error: it would miss, and on a GUID
 * collision it would print an unrelated company's name. So the column is omitted
 * rather than guessed, matching the same rule the employee list applies to its
 * missing company column.
 *
 * `daysRemaining` is rendered with the shared daysRemainingText helper, which is
 * built for the expiry registry. That reuse is exact, not approximate: both the
 * registry and this endpoint select the same SqlSnippets.DaysRemaining expression,
 * FLOOR(EXTRACT(EPOCH FROM (expiry_date - CURRENT_TIMESTAMP)) / 86400), so the
 * number is signed and floored identically. It is never recomputed here.
 *
 * `status` uses the REGISTRY label and tone helpers, not the stored-document
 * ones. This endpoint's status comes from SqlSnippets.ExpiryStatusLabelCase and
 * is therefore the registry vocabulary — Active / ExpiringSoon / Expired. The
 * stored-document palette is keyed on the DocumentStatus enum and maps `Overdue`
 * where this data says `Expired`; feeding one vocabulary into the other is
 * precisely what those helpers warn against.
 *
 * There is no sort control because the endpoint fixes its own ordering.
 */
function AdminEmployeeExpiringTable({ items }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full border-collapse text-left text-sm">
          <caption className="sr-only">
            Employee documents approaching expiry
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
                Document
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
              >
                Expires
              </th>
              <th
                scope="col"
                className="hidden px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] lg:table-cell"
              >
                Time left
              </th>
              <th
                scope="col"
                className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280] last:pr-0"
              >
                Status
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((item) => {
              const name = presentText(item?.fullName)
              const remaining = daysRemainingText(item?.daysRemaining)

              return (
                <tr
                  key={`${item?.documentId}-${item?.expiryDate}`}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-[#F7F8FA]/60"
                >
                  <td className="max-w-[20rem] px-3 py-3.5 align-middle first:pl-0">
                    <div className="flex items-center gap-2">
                      <FileClock
                        size={15}
                        className="shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <span
                        className="min-w-0 break-words font-semibold text-[#16181D]"
                        title={name ?? undefined}
                      >
                        {truncate(name, 52) ?? MISSING_VALUE}
                      </span>
                    </div>
                  </td>
                  <td className="max-w-[16rem] px-3 py-3.5 align-middle text-[#16181D]">
                    <span className="min-w-0 break-words" title={item?.documentNumber}>
                      {truncate(presentText(item?.documentNumber), 40) ?? MISSING_VALUE}
                    </span>
                  </td>
                  <td className="px-3 py-3.5 align-middle whitespace-nowrap text-[#16181D]">
                    {formatDate(item?.expiryDate)}
                  </td>
                  <td className="hidden px-3 py-3.5 align-middle whitespace-nowrap text-[#16181D] lg:table-cell">
                    {remaining ?? <span className="text-[#9CA3AF]">{MISSING_VALUE}</span>}
                  </td>
                  <td className="px-3 py-3.5 align-middle last:pr-0">
                    <StatusPill
                      label={registryStatusLabel(item?.status) ?? 'Unknown'}
                      tone={registryStatusTone(item?.status)}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((item) => {
          const name = presentText(item?.fullName)
          const remaining = daysRemainingText(item?.daysRemaining)

          return (
            <li
              key={`${item?.documentId}-${item?.expiryDate}`}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-center gap-2">
                  <FileClock
                    size={15}
                    className="shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <span className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                    {name ?? MISSING_VALUE}
                  </span>
                </div>
                <StatusPill
                  label={registryStatusLabel(item?.status) ?? 'Unknown'}
                  tone={registryStatusTone(item?.status)}
                />
              </div>

              <dl className="mt-3 flex flex-col gap-1.5">
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">Document</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {displayText(item?.documentNumber)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">Expires</dt>
                  <dd className="whitespace-nowrap text-right text-xs font-medium text-[#16181D]">
                    {formatDate(item?.expiryDate)}
                  </dd>
                </div>
                {remaining && (
                  <div className="flex items-start justify-between gap-3">
                    <dt className="text-xs text-[#6B7280]">Time left</dt>
                    <dd className="text-right text-xs font-medium text-[#16181D]">
                      {remaining}
                    </dd>
                  </div>
                )}
              </dl>
            </li>
          )
        })}
      </ul>
    </>
  )
}

export default AdminEmployeeExpiringTable
