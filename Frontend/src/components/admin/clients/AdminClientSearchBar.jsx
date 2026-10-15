import { Search, X } from 'lucide-react'

/**
 * Search control for the Admin client list.
 *
 * The input is two-state by design: `value` is what the user has typed, `applied`
 * is what the server has actually been asked for. Only submitting the form
 * changes `applied`, so a half-typed company name never fires a request per
 * keystroke, and the Clear button knows whether there is anything to clear.
 *
 * `applied` is handed in already seeded from ?search= by the page, which is what
 * makes the header global search hand off cleanly to this list.
 *
 * PLACEHOLDER IS DELIBERATE. The endpoint matches company name and trade licence
 * number only — not phone, email or emirate — so the affordance must not
 * promise a wider search than the backend performs.
 */
function AdminClientSearchBar({ value, applied, onValueChange, onSearch, onClear }) {
  return (
    <form
      onSubmit={(event) => {
        event.preventDefault()
        onSearch()
      }}
      role="search"
      className="mb-5 flex flex-wrap items-center gap-2"
    >
      <div className="relative min-w-[220px] flex-1">
        <label htmlFor="adminClientSearch" className="sr-only">
          Search client companies by company name or trade licence number
        </label>
        <Search
          size={15}
          className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-[#9CA3AF]"
          aria-hidden="true"
        />
        <input
          id="adminClientSearch"
          type="search"
          value={value}
          onChange={(event) => onValueChange(event.target.value)}
          placeholder="Search by company name or trade licence number…"
          className="w-full rounded-[10px] border border-[#E2E4E9] bg-white py-2 pl-9 pr-3 text-sm text-[#16181D] transition duration-150 placeholder:text-[#9CA3AF] hover:border-[#D5D8DE] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
        />
      </div>

      <button
        type="submit"
        className="shrink-0 rounded-[10px] bg-[#0F9D74] px-4 py-2 text-sm font-semibold text-white transition duration-150 hover:bg-[#0B7D5D] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
      >
        Search
      </button>

      {applied && (
        <button
          type="button"
          onClick={onClear}
          className="inline-flex shrink-0 items-center gap-1.5 rounded-[10px] border border-[#E2E4E9] bg-white px-3.5 py-2 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.3)] cursor-pointer"
        >
          <X size={14} strokeWidth={2} aria-hidden="true" />
          Clear
          <span className="sr-only"> search</span>
        </button>
      )}
    </form>
  )
}

export default AdminClientSearchBar
