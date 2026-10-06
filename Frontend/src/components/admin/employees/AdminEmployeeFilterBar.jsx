/**
 * Filter control for the Admin employee list.
 *
 * THIS BAR DELIBERATELY EXPOSES ONE CONTROL, because one is all the audited
 * contract supports. GET /api/v1/admin/employees binds exactly these parameters:
 * clientId, entityId, isActive, page, pageSize. Of those, only `isActive` is in
 * scope for this phase, so only `isActive` appears here.
 *
 * What is NOT here, and why — each of these would be a control that either does
 * nothing or quietly under-reports:
 *
 *   - A search box. The endpoint accepts NO `search` parameter and the SQL has no
 *     text predicate on any column, so there is nothing to search. A search field
 *     here would be a control that silently fails, which is worse than its
 *     absence. This is a backend gap, not a UI omission.
 *   - A sort control. The query hardcodes ORDER BY e.created_at DESC, e.id ASC.
 *     There is no sort parameter, so no column can be made sortable.
 *   - A "show deleted" toggle. The list hard-filters e.is_deleted = false and
 *     binds no includeDeleted. Only the detail route accepts it.
 *   - A company or legal-entity filter. Both clientId and entityId ARE genuine
 *     server-side filters with filter-correct counts, but scoping this module by
 *     company is explicitly out of scope for this phase, so they are not exposed.
 *     Adding a company combobox would also need a way to change scope later, and
 *     the destination for a scoped employee list is not settled.
 *
 * WHY isActive IS SAFE TO SHIP. It is a real tri-state server parameter — the
 * repository applies `e.is_active = @IsActive` to the same WHERE clause used for
 * the COUNT, so the reported total always matches the rows shown. That is exactly
 * the property the deferred client-side-only filters lack.
 *
 * The control is a fieldset of real radio inputs rather than a row of buttons, so
 * arrow-key navigation, grouping and the announced selected state all come from
 * native semantics instead of being reimplemented. The input is visually hidden
 * but focusable, and focus is shown on the label via peer-focus-visible.
 */
const STATUS_OPTIONS = [
  { value: 'all', label: 'All' },
  { value: 'true', label: 'Active' },
  { value: 'false', label: 'Inactive' },
]

function AdminEmployeeFilterBar({ status, onStatusChange, isFetching }) {
  return (
    <div className="mb-5 flex flex-wrap items-center gap-x-4 gap-y-3">
      <fieldset className="min-w-0">
        <legend className="sr-only">Filter employees by employment status</legend>
        <div
          role="group"
          aria-label="Employment status"
          className="inline-flex flex-wrap gap-1 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA] p-1"
        >
          {STATUS_OPTIONS.map((option) => {
            const inputId = `adminEmployeeStatus-${option.value}`
            const isSelected = status === option.value

            return (
              <div key={option.value} className="flex">
                <input
                  type="radio"
                  id={inputId}
                  name="adminEmployeeStatus"
                  value={option.value}
                  checked={isSelected}
                  onChange={() => onStatusChange(option.value)}
                  className="peer sr-only"
                />
                <label
                  htmlFor={inputId}
                  className={`cursor-pointer rounded-[8px] px-3 py-1.5 text-xs font-semibold transition duration-150 peer-focus-visible:ring-2 peer-focus-visible:ring-[#0F9D74] peer-focus-visible:ring-offset-1 ${
                    isSelected
                      ? 'bg-white text-[#0F9D74] shadow-[0_1px_2px_rgba(28,31,38,0.06)]'
                      : 'text-[#6B7280] hover:text-[#16181D]'
                  }`}
                >
                  {option.label}
                </label>
              </div>
            )
          })}
        </div>
      </fieldset>

      <p className="min-w-0 flex-1 text-xs text-[#6B7280]">
        The employee list has no text search, so this directory cannot be filtered
        by name, passport or job title.
      </p>

      {isFetching && (
        <p className="text-xs text-[#6B7280]" role="status">
          Refreshing…
        </p>
      )}
    </div>
  )
}

export default AdminEmployeeFilterBar
