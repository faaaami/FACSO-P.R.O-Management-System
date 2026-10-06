import { RotateCcw } from 'lucide-react'
import { DOCUMENT_TYPES, DOCUMENT_STATUS } from '../../client/enumLabels'

const TYPE_OPTIONS = Object.entries(DOCUMENT_TYPES)
const STATUS_OPTIONS = Object.entries(DOCUMENT_STATUS)

const controlClass =
  'rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-2 text-sm text-[#16181D] transition duration-150 hover:border-[#D5D8DE] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]'

/**
 * Type / status / expires-before filters for the Agent document list.
 * Purely controlled — the owning page holds filter state and resets paging.
 */
function AgentDocumentFilters({ filters, onChange, onClear, isDirty }) {
  return (
    <form
      onSubmit={(event) => event.preventDefault()}
      className="flex flex-wrap items-end gap-2"
      aria-label="Document filters"
    >
      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-[#6B7280]">Type</span>
        <select
          value={filters.type ?? ''}
          onChange={(event) => onChange({ ...filters, type: event.target.value || undefined })}
          className={controlClass}
        >
          <option value="">All types</option>
          {TYPE_OPTIONS.map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-[#6B7280]">Status</span>
        <select
          value={filters.status ?? ''}
          onChange={(event) => onChange({ ...filters, status: event.target.value || undefined })}
          className={controlClass}
        >
          <option value="">All statuses</option>
          {STATUS_OPTIONS.map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </select>
      </label>

      <label className="flex flex-col gap-1">
        <span className="text-xs font-medium text-[#6B7280]">Expiring before</span>
        <input
          type="date"
          value={filters.expiresBefore ?? ''}
          onChange={(event) => onChange({ ...filters, expiresBefore: event.target.value || undefined })}
          className={controlClass}
        />
      </label>

      {isDirty && (
        <button
          type="button"
          onClick={onClear}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-3 py-2 text-sm font-semibold text-[#6B7280] transition duration-150 hover:bg-[#F7F8FA] hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] cursor-pointer"
        >
          <RotateCcw size={14} strokeWidth={1.75} aria-hidden="true" />
          Clear
        </button>
      )}
    </form>
  )
}

export default AgentDocumentFilters
