import { X } from 'lucide-react'

function Drawer({ title, icon: Icon, subtitle, onClose, children }) {
  return (
    <div className="fixed inset-0 z-50" role="dialog" aria-modal="true">
      <div
        className="fixed inset-0 bg-slate-900/40 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside className="fixed inset-y-0 right-0 z-50 flex w-full max-w-md flex-col bg-white border-l border-[#E2E4E9] shadow-[0_8px_24px_rgba(28,31,38,0.10)]">
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] p-6">
          <div className="min-w-0 flex items-start gap-3">
            {Icon && (
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
                <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
              </div>
            )}
            <div className="min-w-0">
              <h2 className="text-base font-semibold tracking-tight text-[#16181D]">
                {title}
              </h2>
              {subtitle && (
                <p className="mt-0.5 text-xs text-[#6B7280]">{subtitle}</p>
              )}
            </div>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close panel"
            className="shrink-0 rounded-[8px] p-1.5 text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>

        <div className="flex-1 overflow-y-auto p-6">{children}</div>
      </aside>
    </div>
  )
}

export default Drawer