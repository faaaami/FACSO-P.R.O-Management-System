import { History } from 'lucide-react'
import { formatDateTime } from '../../client/billing/format'
import { humanizeEntityType } from './auditLogDisplay'

/**
 * The audit event list.
 *
 * A LIST, NOT A TABLE, and deliberately so. Every row carries a free-text
 * description the schema allows up to 500 characters, which is exactly the
 * column that forces a table to either truncate audit information or scroll
 * sideways on a phone. Stacking each event keeps the full description readable
 * at 375px and keeps the GUIDs wrappable, and it matches the NotificationsList
 * pattern already shipped for the Admin portal.
 *
 * EVERY FIELD RENDERS DEFENSIVELY BECAUSE MOST OF THEM ARE NULLABLE. `userId`
 * is null on unauthenticated failures such as LoginFailed, `userName` is null
 * whenever the LEFT JOIN to users finds nothing, and `employeeId`, `description`
 * and `metadata` are null on most rows. A null is never rendered as a blank
 * line or an empty pill — absent fields are simply not shown, so a sparse row
 * stays compact and a missing actor is never mistaken for a rendering bug.
 *
 * The actor fallback is explicit rather than blank. A LoginFailed row
 * legitimately has no user, and an audit log that silently shows an empty space
 * where the actor should be reads as missing data.
 *
 * NOTHING IS TRUNCATED. Description and metadata wrap with break-words, and a
 * GUID wraps with break-all, because an audit record that hides part of its own
 * content to fit a box is worse than one that is taller. There is deliberately
 * no expand/collapse, no drawer and no JSON viewer: `metadata` is short plain
 * text with a single writer emitting `eventId=…`, and description is capped at
 * 500 characters, so everything fits inline and a disclosure control would only
 * hide content.
 */
function AdminAuditLogList({ items }) {
  return (
    <ul className="space-y-3">
      {items.map((item) => {
        const timestamp = formatDateTime(item?.createdAt)
        const actorName =
          typeof item?.userName === 'string' && item.userName.trim()
            ? item.userName
            : null
        const description =
          typeof item?.description === 'string' && item.description.trim()
            ? item.description
            : null
        const metadata =
          typeof item?.metadata === 'string' && item.metadata.trim()
            ? item.metadata
            : null
        const entityLabel = humanizeEntityType(item?.entityType)

        return (
          <li
            key={item.id}
            className="flex flex-col gap-3 rounded-[10px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_2px_rgba(28,31,38,0.04)] sm:flex-row sm:items-start sm:justify-between"
          >
            <div className="flex min-w-0 flex-1 items-start gap-3">
              <span
                className="mt-1 flex h-7 w-7 shrink-0 items-center justify-center rounded-[8px] border border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]"
                aria-hidden="true"
              >
                <History size={14} strokeWidth={1.75} />
              </span>

              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
                  <p className="text-sm font-semibold tracking-tight text-[#16181D]">
                    {item.action}
                  </p>
                  {actorName ? (
                    <p className="text-xs font-medium text-[#6B7280]">
                      {actorName}
                    </p>
                  ) : (
                    <p className="text-xs font-medium text-[#9CA3AF]">
                      No user recorded
                    </p>
                  )}
                </div>

                {timestamp && (
                  <p className="mt-0.5 text-xs text-[#9CA3AF]">{timestamp}</p>
                )}

                {entityLabel && (
                  <p className="mt-1.5 text-xs font-medium text-[#16181D]">
                    {entityLabel}
                  </p>
                )}

                {description && (
                  <p className="mt-1 break-words text-[13px] leading-snug text-[#6B7280]">
                    {description}
                  </p>
                )}

                {metadata && (
                  <p className="mt-1.5 break-words text-xs text-[#6B7280]">
                    <span className="font-semibold text-[#16181D]">Metadata: </span>
                    {metadata}
                  </p>
                )}

                <div className="mt-2 flex flex-wrap items-center gap-x-3 gap-y-1">
                  <p className="break-all font-mono text-[11px] text-[#9CA3AF]">
                    {item.entityId}
                  </p>
                  {item.employeeId && (
                    <p className="break-all font-mono text-[11px] text-[#9CA3AF]">
                      <span className="font-semibold">Employee: </span>
                      {item.employeeId}
                    </p>
                  )}
                </div>
              </div>
            </div>
          </li>
        )
      })}
    </ul>
  )
}

export default AdminAuditLogList
