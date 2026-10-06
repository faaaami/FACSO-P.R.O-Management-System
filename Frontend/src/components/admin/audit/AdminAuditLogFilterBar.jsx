import { useEffect, useState } from 'react'
import { RotateCcw, Search } from 'lucide-react'
import {
  ENTITY_TYPE_OPTIONS,
  OTHER_ENTITY_TYPE,
  isGuid,
} from './auditLogDisplay'

/**
 * Filter bar for the Admin audit log.
 *
 * DRAFT STATE IS THE WHOLE POINT OF THIS COMPONENT. Everything typed here is
 * local; nothing reaches the query until Apply is pressed. Two reasons.
 *
 * First, these are SERVER-side exact-match filters over a system-wide table, not
 * the client-side row matching the other Admin filter bars do over already
 * loaded rows. Committing on every keystroke would fire a request per character
 * and per half-typed GUID.
 *
 * Second, `entityId` and `userId` are GUIDs. Committing those as they are typed
 * would guarantee a stream of 400 ProblemDetails for every partial id.
 *
 * So the flow is: type -> Apply -> validate -> the PAGE commits and resets to
 * page 1 -> TanStack Query fires. This component never queries anything itself
 * and never sees a page number.
 *
 * VALIDATION IS ADVISORY, NOT AUTHORITATIVE. The GUID shape check and the
 * from <= to check exist to keep obviously-invalid input off the wire. The
 * backend still validates, and if it ever rejects a range its message is
 * surfaced through the existing error path.
 *
 * WHY "Other…" EXISTS. ENTITY_TYPE_OPTIONS is a snapshot of the 17 strings the
 * backend persists today, and there is no endpoint that enumerates them, so the
 * list will go stale. The escape hatch keeps a type added later reachable. The
 * sentinel is stripped before the value is committed and is never sent.
 *
 * The override is sent EXACTLY as typed — not trimmed, not lowercased, not
 * title-cased. Silently normalising it would defeat the point of a control for
 * a case-sensitive comparison, and would make the request disagree with the
 * field the user is looking at.
 */
const EMPTY_DRAFT = {
  entityType: '',
  entityTypeOther: '',
  entityId: '',
  userId: '',
  from: '',
  to: '',
}

function AdminAuditLogFilterBar({ filters, onApply, onReset, isFetching }) {
  const [draft, setDraft] = useState(EMPTY_DRAFT)
  const [errors, setErrors] = useState({})

  // Re-sync when the page's committed filters change underneath us, which is
  // what Reset does. Without this, clearing the filters would clear the request
  // but leave the boxes populated.
  useEffect(() => {
    setDraft({ ...EMPTY_DRAFT, ...filters })
    setErrors({})
  }, [filters])

  const isOther = draft.entityType === OTHER_ENTITY_TYPE
  const hasActiveFilters =
    Object.values(filters).some((value) => typeof value === 'string' && value.trim())

  function update(field, value) {
    setDraft((current) => ({ ...current, [field]: value }))
    // Clearing a field's error as soon as it is edited keeps a stale red border
    // from sitting under a value the user has already corrected.
    setErrors((current) => {
      if (!current[field]) return current
      const next = { ...current }
      delete next[field]
      return next
    })
  }

  function handleEntityTypeChange(value) {
    if (value === OTHER_ENTITY_TYPE) {
      // Switching INTO "Other…" keeps any previously typed override.
      setDraft((current) => ({ ...current, entityType: OTHER_ENTITY_TYPE }))
    } else {
      // Switching AWAY discards the override, so a stale custom value can never
      // be committed behind a visible select that says something else.
      setDraft((current) => ({
        ...current,
        entityType: value,
        entityTypeOther: '',
      }))
    }
    setErrors((current) => {
      const next = { ...current }
      delete next.entityType
      delete next.entityTypeOther
      return next
    })
  }

  function handleApply() {
    const nextErrors = {}

    if (isOther && !draft.entityTypeOther.trim()) {
      nextErrors.entityTypeOther = 'Enter an entity type.'
    }

    if (draft.entityId.trim() && !isGuid(draft.entityId)) {
      nextErrors.entityId = 'Enter a valid GUID, or leave this blank.'
    }

    if (draft.userId.trim() && !isGuid(draft.userId)) {
      nextErrors.userId = 'Enter a valid GUID, or leave this blank.'
    }

    if (draft.from && draft.to && draft.from > draft.to) {
      // YYYY-MM-DD is lexicographically ordered, so a plain string compare is a
      // correct date compare here and avoids constructing Dates just to compare.
      nextErrors.to = 'The To date must not be earlier than the From date.'
    }

    setErrors(nextErrors)
    if (Object.keys(nextErrors).length > 0) return

    // "Other…" is committed as the sentinel PLUS the override as two separate
    // fields, and the PAGE resolves the pair down to the single wire value. The
    // tempting shortcut is to collapse them here into `entityType: override`, but
    // that loses the mode: on the next re-sync `entityType` would hold a string
    // that is neither one of the 17 options nor the sentinel, so the select would
    // render blank, the override input would be hidden (isOther is now false),
    // and the required-field check above would stop applying. The active filter
    // would still be sent correctly but would be invisible and impossible to
    // edit. The override is trimmed here only to guarantee it is non-blank, which
    // the check above already enforced; the value itself is sent verbatim.
    onApply({
      entityType: isOther ? OTHER_ENTITY_TYPE : draft.entityType,
      entityTypeOther: isOther ? draft.entityTypeOther.trim() : '',
      entityId: draft.entityId.trim(),
      userId: draft.userId.trim(),
      from: draft.from,
      to: draft.to,
    })
  }

  function handleReset() {
    setDraft({ ...EMPTY_DRAFT })
    setErrors({})
    onReset()
  }

  const fieldClass =
    'block w-full rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-2 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]'
  const errorClass = 'border-[#DC2626] focus:border-[#DC2626] focus:ring-[rgba(220,38,38,0.15)]'
  const labelClass = 'mb-1.5 block text-xs font-medium text-[#6B7280]'

  return (
    <div className="mb-5 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 p-4">
      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <div className="min-w-0">
          <label htmlFor="adminAuditEntityType" className={labelClass}>
            Entity type
          </label>
          <select
            id="adminAuditEntityType"
            value={draft.entityType}
            onChange={(event) => handleEntityTypeChange(event.target.value)}
            aria-describedby="adminAuditEntityType-hint"
            className={fieldClass}
          >
            <option value="">All entity types</option>
            {ENTITY_TYPE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
            <option value={OTHER_ENTITY_TYPE}>Other…</option>
          </select>
          <p id="adminAuditEntityType-hint" className="mt-1 text-[11px] text-[#9CA3AF]">
            Exact, case-sensitive match.
          </p>
        </div>

        {isOther && (
          <div className="min-w-0">
            <label htmlFor="adminAuditEntityTypeOther" className={labelClass}>
              Custom entity type
            </label>
            <input
              id="adminAuditEntityTypeOther"
              type="text"
              value={draft.entityTypeOther}
              onChange={(event) => update('entityTypeOther', event.target.value)}
              placeholder="Exact stored value"
              spellCheck={false}
              autoComplete="off"
              aria-invalid={errors.entityTypeOther ? 'true' : undefined}
              aria-describedby={
                errors.entityTypeOther
                  ? 'adminAuditEntityTypeOther-error'
                  : 'adminAuditEntityTypeOther-hint'
              }
              className={`${fieldClass} ${errors.entityTypeOther ? errorClass : ''}`}
            />
            {errors.entityTypeOther ? (
              <p
                id="adminAuditEntityTypeOther-error"
                className="mt-1 text-[11px] font-medium text-[#DC2626]"
              >
                {errors.entityTypeOther}
              </p>
            ) : (
              <p id="adminAuditEntityTypeOther-hint" className="mt-1 text-[11px] text-[#9CA3AF]">
                Sent exactly as typed. Case matters.
              </p>
            )}
          </div>
        )}

        <div className="min-w-0">
          <label htmlFor="adminAuditEntityId" className={labelClass}>
            Entity ID
          </label>
          <input
            id="adminAuditEntityId"
            type="text"
            value={draft.entityId}
            onChange={(event) => update('entityId', event.target.value)}
            placeholder="GUID"
            spellCheck={false}
            autoComplete="off"
            className={`${fieldClass} font-mono ${errors.entityId ? errorClass : ''}`}
            aria-invalid={errors.entityId ? 'true' : undefined}
            aria-describedby={errors.entityId ? 'adminAuditEntityId-error' : undefined}
          />
          {errors.entityId && (
            <p
              id="adminAuditEntityId-error"
              className="mt-1 text-[11px] font-medium text-[#DC2626]"
            >
              {errors.entityId}
            </p>
          )}
        </div>

        <div className="min-w-0">
          <label htmlFor="adminAuditUserId" className={labelClass}>
            User ID
          </label>
          <input
            id="adminAuditUserId"
            type="text"
            value={draft.userId}
            onChange={(event) => update('userId', event.target.value)}
            placeholder="GUID"
            spellCheck={false}
            autoComplete="off"
            className={`${fieldClass} font-mono ${errors.userId ? errorClass : ''}`}
            aria-invalid={errors.userId ? 'true' : undefined}
            aria-describedby={errors.userId ? 'adminAuditUserId-error' : undefined}
          />
          {errors.userId && (
            <p
              id="adminAuditUserId-error"
              className="mt-1 text-[11px] font-medium text-[#DC2626]"
            >
              {errors.userId}
            </p>
          )}
        </div>

        <div className="min-w-0">
          <label htmlFor="adminAuditFrom" className={labelClass}>
            From
          </label>
          <input
            id="adminAuditFrom"
            type="date"
            value={draft.from}
            onChange={(event) => update('from', event.target.value)}
            className={fieldClass}
          />
        </div>

        <div className="min-w-0">
          <label htmlFor="adminAuditTo" className={labelClass}>
            To
          </label>
          <input
            id="adminAuditTo"
            type="date"
            value={draft.to}
            onChange={(event) => update('to', event.target.value)}
            aria-invalid={errors.to ? 'true' : undefined}
            aria-describedby={
              errors.to ? 'adminAuditTo-error' : 'adminAuditDateHint'
            }
            className={`${fieldClass} ${errors.to ? errorClass : ''}`}
          />
          {errors.to ? (
            <p id="adminAuditTo-error" className="mt-1 text-[11px] font-medium text-[#DC2626]">
              {errors.to}
            </p>
          ) : (
            <p id="adminAuditDateHint" className="mt-1 text-[11px] text-[#9CA3AF]">
              Inclusive, by calendar day.
            </p>
          )}
        </div>
      </div>

      <div className="mt-4 flex flex-wrap items-center justify-between gap-3">
        <p className="max-w-xl text-[11px] leading-relaxed text-[#9CA3AF]">
          These are exact-match filters. There is no text search or action filter. Audit
          events are shown newest first and the order cannot be changed.
        </p>

        <div className="flex shrink-0 flex-wrap items-center gap-2">
          {isFetching && (
            <span className="inline-flex items-center gap-1.5 text-[11px] font-medium text-[#6B7280]">
              <Search size={12} strokeWidth={2} className="animate-pulse" aria-hidden="true" />
              Updating…
            </span>
          )}

          <button
            type="button"
            onClick={handleReset}
            disabled={!hasActiveFilters && !isOther}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
          >
            <RotateCcw size={13} strokeWidth={2} aria-hidden="true" />
            Reset
          </button>

          <button
            type="button"
            onClick={handleApply}
            className="inline-flex cursor-pointer items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2"
          >
            Apply
          </button>
        </div>
      </div>
    </div>
  )
}

export default AdminAuditLogFilterBar
