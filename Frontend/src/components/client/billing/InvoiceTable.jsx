import { Eye } from 'lucide-react'
import LoadingState from '../LoadingState'
import ErrorState from '../ErrorState'
import EmptyState from '../EmptyState'
import StatusPill from '../StatusPill'
import Pagination from './Pagination'

function DetailsButton({ onSelect }) {
  return (
    <button
      type="button"
      onClick={onSelect}
      className="inline-flex items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
    >
      <Eye size={13} strokeWidth={2} aria-hidden="true" />
      Details
    </button>
  )
}

function MobileRow({ item, columns, statusLabel, statusTone, actions }) {
  return (
    <li className="flex flex-col gap-3 rounded-[10px] bg-white border border-[#E2E4E9] p-4 sm:hidden">
      <div className="flex items-center justify-between gap-3">
        <StatusPill label={statusLabel(item)} tone={statusTone(item)} />
        <span className="text-xs font-medium text-[#9CA3AF]">#{item.id.slice(0, 8)}</span>
      </div>
      {columns.length > 0 && (
        <div className="space-y-1.5 text-xs">
          {columns.map((column) => {
            const value = column.render(item)
            return (
              <div key={column.key} className="flex items-start justify-between gap-3">
                <span className="shrink-0 text-[#6B7280]">{column.header}</span>
                <span className="text-right font-medium text-[#16181D]">{value ?? '–'}</span>
              </div>
            )
          })}
        </div>
      )}
      {actions != null && (
        <div className="flex flex-wrap items-center justify-end gap-2">{actions}</div>
      )}
    </li>
  )
}

function InvoiceTable({
  config,
  data,
  isLoading,
  isError,
  onRetry,
  page,
  pageSize,
  onPageChange,
  onSelect,
  renderActions,
}) {
  const { icon: Icon, columns = [] } = config
  const hasActions = renderActions !== null

  const renderRowActions = (item) => {
    if (renderActions) {
      return <div className="inline-flex flex-wrap items-center justify-end gap-2">{renderActions(item)}</div>
    }
    return <DetailsButton onSelect={() => onSelect(item.id)} />
  }

  let content

  if (isLoading) {
    content = <LoadingState label={`Loading ${config.title}…`} />
  } else if (isError) {
    content = <ErrorState message={`Could not load ${config.title}.`} onRetry={onRetry} />
  } else if (!data?.items?.length) {
    content = (
      <EmptyState
        icon={config.emptyIcon ?? Icon}
        message={config.emptyMessage ?? `No ${config.title.toLowerCase()} found.`}
        description={config.emptyDescription}
      />
    )
  } else {
    content = (
      <>
        {/* Mobile card list */}
        <ul className="space-y-3 sm:hidden">
          {data.items.map((item) => (
            <MobileRow
              key={item.id}
              item={item}
              columns={columns}
              statusLabel={config.statusLabel}
              statusTone={config.statusTone}
              actions={hasActions ? renderRowActions(item) : null}
            />
          ))}
        </ul>

        {/* Desktop table */}
        <div className="hidden sm:block overflow-x-auto">
          <table className="w-full">
            <thead>
              <tr>
                <th className="bg-gray-50 text-left text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                  Status
                </th>
                {columns.map((column) => (
                  <th
                    key={column.key}
                    className={`bg-gray-50 text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3 ${
                      column.align === 'right' ? 'text-right' : 'text-left'
                    }`}
                  >
                    {column.header}
                  </th>
                ))}
                {hasActions && (
                  <th className="bg-gray-50 text-right text-xs font-semibold text-[#6B7280] uppercase tracking-wider border-b border-[#E2E4E9] px-4 py-3">
                    <span className="sr-only">Actions</span>
                  </th>
                )}
              </tr>
            </thead>
            <tbody className="divide-y divide-[#E2E4E9]">
              {data.items.map((item) => (
                <tr key={item.id} className="hover:bg-gray-50/60 transition-colors">
                  <td className="px-4 py-3.5 text-sm text-[#16181D]">
                    <StatusPill label={config.statusLabel(item)} tone={config.statusTone(item)} />
                  </td>
                  {columns.map((column) => (
                    <td
                      key={column.key}
                      className={`px-4 py-3.5 text-sm text-[#16181D] ${
                        column.align === 'right' ? 'text-right' : 'text-left'
                      }`}
                    >
                      {column.render(item) ?? <span className="text-[#9CA3AF]">–</span>}
                    </td>
                  ))}
                  {hasActions && <td className="px-4 py-3.5 text-right">{renderRowActions(item)}</td>}
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <Pagination
          page={page}
          pageSize={pageSize}
          totalCount={data.totalCount}
          itemLabel={config.itemNoun}
          onPageChange={onPageChange}
        />
      </>
    )
  }

  return (
    <section
      aria-label={`${config.title} list`}
      className="rounded-[12px] bg-white border border-[#E2E4E9] p-6 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
    >
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
            <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
          </div>
          <div className="min-w-0">
            <h2 className="text-base font-semibold tracking-tight text-[#16181D]">{config.title}</h2>
            <p className="mt-0.5 text-xs text-[#6B7280]">{config.description}</p>
          </div>
        </div>
        {data?.totalCount != null && (
          <span className="shrink-0 rounded-[6px] bg-[#F7F8FA] px-2 py-0.5 text-xs font-semibold text-[#6B7280] border border-[#E2E4E9]">
            {data.totalCount}
          </span>
        )}
      </div>

      {content}
    </section>
  )
}

export default InvoiceTable

export { DetailsButton }