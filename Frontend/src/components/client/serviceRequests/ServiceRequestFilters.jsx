import { Filter, RotateCcw } from 'lucide-react'
import { SERVICE_REQUEST_STATUS } from '../enumLabels'

const selectClasses =
  'h-10 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm text-[#16181D] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none transition duration-150'

function ServiceRequestFilters({ filters, onChange, onClear, isDirty }) {
  const update = (key, value) => {
    onChange({ ...filters, [key]: value || undefined })
  }

  return (
    <section aria-label="Filter service requests" className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
      <div className="flex items-center justify-between gap-4 mb-5">
        <div className="flex items-center gap-2.5">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <Filter size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div>
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">Filters</h2>
            <p className="text-xs text-[#6B7280]">Narrow the service request trail</p>
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

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
        <label className="flex flex-col gap-1.5">
          <span className="text-xs font-medium text-[#6B7280]">Status</span>
          <select
            className={selectClasses}
            value={filters.status ?? ''}
            onChange={(e) => update('status', e.target.value)}
          >
            <option value="">All statuses</option>
            {Object.keys(SERVICE_REQUEST_STATUS).map((key) => (
              <option key={key} value={key}>
                {SERVICE_REQUEST_STATUS[key]}
              </option>
            ))}
          </select>
        </label>
      </div>
    </section>
  )
}

export default ServiceRequestFilters