import { Filter, RotateCcw } from 'lucide-react'
import { DOCUMENT_TYPES, DOCUMENT_STATUS } from '../enumLabels'

const OWNER_TYPES = ['Entity', 'Employee']

const selectClasses =
  'h-10 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function Field({ label, children }) {
  return (
    <label className="flex flex-col gap-1.5">
      <span className="text-xs font-medium text-[#6B7280]">{label}</span>
      {children}
    </label>
  )
}

function DocumentFilters({ filters, onChange, onClear, isDirty }) {
  const update = (key, value) => {
    const next = { ...filters, [key]: value || undefined }
    if (key === 'expiresFrom' && next.expiresTo && next.expiresFrom > next.expiresTo) {
      next.expiresTo = next.expiresFrom
    }
    if (key === 'expiresTo' && next.expiresFrom && next.expiresTo < next.expiresFrom) {
      next.expiresFrom = next.expiresTo
    }
    onChange(next)
  }

  return (
    <section aria-label="Filter documents" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-center justify-between gap-4 mb-5">
        <div className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <Filter size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div>
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Filters</h2>
            <p className="text-xs text-[#6B7280]">Narrow the document register</p>
          </div>
        </div>
        {isDirty && (
          <button
            type="button"
            onClick={onClear}
            className="inline-flex items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-1.5 text-xs font-semibold text-[#6B7280] hover:bg-gray-50 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
          >
            <RotateCcw size={13} strokeWidth={2} aria-hidden="true" />
            Clear
          </button>
        )}
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-5">
        <Field label="Type">
          <select
            className={selectClasses}
            value={filters.type ?? ''}
            onChange={(e) => update('type', e.target.value)}
          >
            <option value="">All types</option>
            {Object.keys(DOCUMENT_TYPES).map((key) => (
              <option key={key} value={key}>
                {DOCUMENT_TYPES[key]}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Status">
          <select
            className={selectClasses}
            value={filters.status ?? ''}
            onChange={(e) => update('status', e.target.value)}
          >
            <option value="">All statuses</option>
            {Object.keys(DOCUMENT_STATUS).map((key) => (
              <option key={key} value={key}>
                {DOCUMENT_STATUS[key]}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Owner Type">
          <select
            className={selectClasses}
            value={filters.ownerType ?? ''}
            onChange={(e) => update('ownerType', e.target.value)}
          >
            <option value="">All owners</option>
            {OWNER_TYPES.map((key) => (
              <option key={key} value={key}>
                {key}
              </option>
            ))}
          </select>
        </Field>

        <Field label="Expiry From">
          <input
            type="date"
            className={selectClasses}
            value={filters.expiresFrom ?? ''}
            onChange={(e) => update('expiresFrom', e.target.value)}
          />
        </Field>

        <Field label="Expiry To">
          <input
            type="date"
            className={selectClasses}
            value={filters.expiresTo ?? ''}
            onChange={(e) => update('expiresTo', e.target.value)}
          />
        </Field>
      </div>
    </section>
  )
}

export default DocumentFilters