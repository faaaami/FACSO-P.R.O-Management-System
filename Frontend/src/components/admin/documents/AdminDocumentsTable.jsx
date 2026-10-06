import { Building2, FileText, UserRound } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import { displayText, formatDate, truncate } from '../clients/clientDisplay'
import { unresolvedOwnerLabel } from '../../../hooks/admin/useAdminDocumentOwners'
import {
  daysRemainingText,
  documentTitle,
  documentTypeLabel,
  registryStatusLabel,
  registryStatusTone,
} from './documentDisplay'

/**
 * The Expiry & Renewal Registry table.
 *
 * BACKING QUERY: GET /api/v1/admin/documents/expiring
 *
 * COLUMNS ARE EXACTLY THE ROW FIELDS. ExpiringDocumentListItemDto carries id,
 * clientEntityId, employeeId, type, documentNumber, issueDate, expiryDate,
 * fileName, isActive, isDeleted, daysRemaining and status — and that is the whole
 * list. So there is deliberately no column for: a renewal status (no such field
 * exists), an owner role or job title (not on this DTO), an entity or company
 * NAME (the row carries owner GUIDs only, resolved separately and explicitly marked
 * unresolved when it cannot be), a version number (that belongs to the versions
 * endpoint), or a file size / content type / issue date in the list view (the row
 * has fileName but not the other file fields).
 *
 * THE ROW HAS NO fileUrl, SO THERE IS NO "VIEW SCAN" ACTION HERE. The scan route
 * rejects a document whose fileUrl is blank, and a registry row cannot tell us
 * whether one is set — offering the button here would manufacture an error for
 * every file-less document. The scan action therefore lives in the detail drawer,
 * where GetDocumentByIdResponseDto does expose fileUrl and the button can be gated
 * on it honestly. The single row action is "Details".
 *
 * OWNER ATTRIBUTION IS DEGRADED, NEVER GUESSED. `resolveOwner` returns a descriptor
 * for every row; an owner that cannot be resolved renders as an explicitly
 * labelled short GUID rather than as a fabricated name. See
 * useAdminDocumentOwners for why an entity-only document's owner is unreachable
 * through the Admin API.
 *
 * STATUS IS THE REGISTRY'S OWN VALUE. The pill shows exactly what the endpoint
 * returned, mapped only for display wording. The status is not re-derived from
 * daysRemaining or from the selected window: the server's label uses a hardcoded
 * 90-day threshold that is independent of the `days` filter, so a locally
 * recomputed status would contradict the pill beside it.
 *
 * ORDERING IS THE BACKEND'S. Rows arrive `ORDER BY expiry_date ASC` and are
 * rendered in that order. The query has no tie-breaker, so two documents sharing an
 * expiry date have no defined relative order — that is a documented backend
 * limitation and is NOT compensated for here, because sorting the received page
 * would not make the pagination itself any more consistent.
 *
 * RESPONSIVE: a real <table> from `md` up inside an overflow container, and a
 * stacked card list below it. Both are rendered by the same row data, and neither
 * carries an element id, so there are no duplicate ids between the two
 * representations.
 */
function AdminDocumentsTable({ items, resolveOwner, onOpenDetails }) {
  function renderOwner(document) {
    const owner = resolveOwner?.(document)

    if (!owner || owner.kind === 'none') {
      return <span className="text-[#9CA3AF]">{displayText(null)}</span>
    }

    if (owner.resolved) {
      return (
        <div className="flex min-w-0 items-start gap-1.5">
          <UserRound
            size={13}
            strokeWidth={1.75}
            className="mt-0.5 shrink-0 text-[#6B7280]"
            aria-hidden="true"
          />
          <div className="min-w-0">
            <p className="min-w-0 break-words font-medium text-[#16181D]">
              {owner.name}
            </p>
            {owner.entityName && (
              <p className="min-w-0 break-words text-xs text-[#6B7280]">
                {owner.entityName}
              </p>
            )}
          </div>
        </div>
      )
    }

    // Not resolved. The short id is labelled as unresolved so it can never be
    // mistaken for a name; the full value stays available in the tooltip.
    return (
      <div className="flex min-w-0 items-start gap-1.5" title={String(owner.id)}>
        <Building2
          size={13}
          strokeWidth={1.75}
          className="mt-0.5 shrink-0 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <div className="min-w-0">
          <p className="min-w-0 break-words text-[#9CA3AF]">
            {unresolvedOwnerLabel(owner)}
          </p>
          <p className="text-xs text-[#9CA3AF]">
            {owner.kind === 'entity'
              ? 'legal entity, not resolvable in Admin'
              : 'not resolvable in Admin'}
          </p>
        </div>
      </div>
    )
  }

  function renderStatus(document) {
    return (
      <StatusPill
        label={registryStatusLabel(document?.status) ?? 'Unknown'}
        tone={registryStatusTone(document?.status)}
      />
    )
  }

  function renderTiming(document) {
    const text = daysRemainingText(document?.daysRemaining)
    return (
      <span className="text-xs text-[#6B7280]">
        {text ?? <span className="text-[#9CA3AF]">{displayText(null)}</span>}
      </span>
    )
  }

  return (
    <>
      {/* Desktop: a real table, so column headers are announced with their cells. */}
      <div className="hidden overflow-x-auto rounded-[10px] border border-[#E2E4E9] md:block">
        <table className="w-full min-w-[720px] border-collapse text-left">
          <caption className="sr-only">
            Documents expiring within the selected window, soonest expiry first.
          </caption>
          <thead className="bg-gray-50">
            <tr>
              <th scope="col" className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Document
              </th>
              <th scope="col" className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Owner
              </th>
              <th scope="col" className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Expiry date
              </th>
              <th scope="col" className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Timing
              </th>
              <th scope="col" className="px-4 py-3 text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                Status
              </th>
              <th scope="col" className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {items.map((document) => {
              const title = documentTitle(document)

              return (
                <tr
                  key={document?.id}
                  className="border-t border-[#E2E4E9] transition-colors hover:bg-gray-50/60"
                >
                  <td className="px-4 py-3.5 align-top">
                    <div className="flex min-w-0 items-start gap-2.5">
                      <FileText
                        size={15}
                        strokeWidth={1.75}
                        className="mt-0.5 shrink-0 text-[#6B7280]"
                        aria-hidden="true"
                      />
                      <div className="min-w-0">
                        <p
                          className="min-w-0 break-words text-sm font-semibold text-[#16181D]"
                          title={title}
                        >
                          {truncate(title, 46) ?? title}
                        </p>
                        <p className="text-xs text-[#6B7280]">
                          {documentTypeLabel(document?.type) ?? 'Document'}
                        </p>
                      </div>
                    </div>
                  </td>
                  <td className="px-4 py-3.5 align-top text-sm">{renderOwner(document)}</td>
                  <td className="whitespace-nowrap px-4 py-3.5 align-top text-sm text-[#16181D]">
                    {formatDate(document?.expiryDate)}
                  </td>
                  <td className="whitespace-nowrap px-4 py-3.5 align-top">
                    {renderTiming(document)}
                  </td>
                  <td className="px-4 py-3.5 align-top">{renderStatus(document)}</td>
                  <td className="px-4 py-3.5 text-right align-top">
                    <button
                      type="button"
                      onClick={() => onOpenDetails(document.id)}
                      className="inline-flex shrink-0 cursor-pointer items-center rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                    >
                      Details
                      <span className="sr-only"> for {title}</span>
                    </button>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      {/* Mobile: the same rows as stacked cards. */}
      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((document) => {
          const title = documentTitle(document)

          return (
            <li
              key={document?.id}
              className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-3.5"
            >
              <div className="flex items-start justify-between gap-3">
                <div className="flex min-w-0 items-start gap-2.5">
                  <FileText
                    size={15}
                    strokeWidth={1.75}
                    className="mt-0.5 shrink-0 text-[#6B7280]"
                    aria-hidden="true"
                  />
                  <div className="min-w-0">
                    <p className="break-words text-sm font-semibold text-[#16181D]">
                      {title}
                    </p>
                    <p className="text-xs text-[#6B7280]">
                      {documentTypeLabel(document?.type) ?? 'Document'}
                    </p>
                  </div>
                </div>
                {renderStatus(document)}
              </div>

              <dl className="mt-2.5 flex flex-col gap-1.5">
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">Owner</dt>
                  <dd className="min-w-0 text-right text-xs font-medium text-[#16181D]">
                    {renderOwner(document)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">Expiry date</dt>
                  <dd className="min-w-0 break-words text-right text-xs font-medium text-[#16181D]">
                    {formatDate(document?.expiryDate)}
                  </dd>
                </div>
                <div className="flex items-start justify-between gap-3">
                  <dt className="text-xs text-[#6B7280]">Timing</dt>
                  <dd className="min-w-0 text-right text-xs font-medium text-[#16181D]">
                    {renderTiming(document)}
                  </dd>
                </div>
              </dl>

              <button
                type="button"
                onClick={() => onOpenDetails(document.id)}
                className="mt-3 inline-flex w-full cursor-pointer items-center justify-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
              >
                Details
                <span className="sr-only"> for {title}</span>
              </button>
            </li>
          )
        })}
      </ul>
    </>
  )
}

export default AdminDocumentsTable
