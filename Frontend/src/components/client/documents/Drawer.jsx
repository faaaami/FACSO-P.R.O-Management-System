import { useId } from 'react'
import { X } from 'lucide-react'
import { useFocusTrap } from '../../../hooks/useFocusTrap'

/**
 * Shared slide-over panel.
 *
 * `size` only changes the panel's max width; every existing caller keeps the
 * original `md` (max-w-md) and is unaffected. The `lg` variant exists for the
 * document review drawer, whose dynamic per-type field grid does not fit in a
 * 448px column without collapsing into one field per row.
 */
const WIDTH_CLASSES = {
  md: 'max-w-md',
  lg: 'max-w-2xl',
}

function Drawer({ title, icon: Icon, subtitle, onClose, size = 'md', children }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const widthClass = WIDTH_CLASSES[size] ?? WIDTH_CLASSES.md

  return (
    <div className="fixed inset-0 z-50">
      <div
        className="fixed inset-0 bg-slate-900/40 transition-opacity"
        onClick={onClose}
        aria-hidden="true"
      />

      <aside
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={`fixed inset-y-0 right-0 z-50 flex w-full ${widthClass} flex-col bg-white border-l border-[#E2E4E9] shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] p-6">
          <div className="min-w-0 flex items-start gap-3">
            {Icon && (
              <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
                <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
              </div>
            )}
            <div className="min-w-0">
              <h2 id={titleId} className="text-base font-semibold tracking-tight text-[#16181D]">
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
