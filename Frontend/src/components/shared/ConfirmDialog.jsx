import { useCallback, useId } from 'react'
import { AlertTriangle, Info, Loader2, ShieldAlert } from 'lucide-react'
import { useFocusTrap } from '../../hooks/useFocusTrap'

/**
 * Generic confirmation dialog for irreversible or consequential actions.
 *
 * Generic presentation only: it owns no data fetching and no mutation state.
 * The caller passes `isLoading` while its own request is in flight.
 *
 * Follows DESIGN.md — 12px modal radius, hairline #E2E4E9 border, restrained
 * enterprise shadow, Lucide icons, 150ms transitions, no gradients or
 * glassmorphism.
 *
 * Accessibility: `role="dialog"` + `aria-modal`, labelled by its title and
 * described by its message, Escape and overlay click cancel, and Tab is trapped
 * via the shared useFocusTrap hook (which also locks body scroll and restores
 * focus to the trigger on close). Cancel is rendered first so the hook's
 * focus-on-open lands on the safe choice for destructive tones.
 */
const TONE_STYLES = {
  default: {
    Icon: Info,
    iconWrap: 'border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]',
    confirm: 'bg-[#1C1F26] text-white hover:bg-[#101319]',
  },
  warning: {
    Icon: AlertTriangle,
    iconWrap: 'border-[#D97706]/20 bg-amber-50 text-[#D97706]',
    confirm: 'bg-[#D97706] text-white hover:bg-[#B45309]',
  },
  danger: {
    Icon: ShieldAlert,
    iconWrap: 'border-[#DC2626]/20 bg-red-50 text-[#DC2626]',
    confirm: 'bg-[#DC2626] text-white hover:bg-[#B91C1C]',
  },
}

const BUTTON_BASE =
  'inline-flex items-center justify-center gap-2 rounded-[10px] px-4 py-2.5 text-sm font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50'

function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = 'Confirm',
  cancelLabel = 'Cancel',
  tone = 'default',
  isLoading = false,
  onConfirm,
  onCancel,
}) {
  const titleId = useId()
  const messageId = useId()

  const styles = TONE_STYLES[tone] ?? TONE_STYLES.default
  const { Icon } = styles

  // While a mutation is in flight the dialog stays open: cancelling here would
  // hide the outcome of a request the caller already started.
  const handleCancel = useCallback(() => {
    if (isLoading) return
    onCancel?.()
  }, [isLoading, onCancel])

  const panelRef = useFocusTrap({ isOpen: open, onClose: handleCancel })

  const handleConfirm = () => {
    if (isLoading) return
    onConfirm?.()
  }

  if (!open) {
    return null
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={handleCancel}
        aria-hidden="true"
      />

      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        aria-describedby={message ? messageId : undefined}
        aria-busy={isLoading || undefined}
        tabIndex={-1}
        className="relative w-full max-w-md rounded-[12px] border border-[#E2E4E9] bg-white p-6 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none"
      >
        <div className="flex items-start gap-3">
          <span
            className={`flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] border ${styles.iconWrap}`}
          >
            <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
          </span>
          <div className="min-w-0 flex-1">
            <h2 id={titleId} className="text-base font-semibold tracking-tight text-[#16181D]">
              {title}
            </h2>
            {message && (
              <div id={messageId} className="mt-1.5 text-sm text-[#6B7280]">
                {message}
              </div>
            )}
          </div>
        </div>

        <div className="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
          <button
            type="button"
            onClick={handleCancel}
            disabled={isLoading}
            className={`${BUTTON_BASE} border border-[#E2E4E9] bg-white text-[#16181D] hover:bg-gray-50`}
          >
            {cancelLabel}
          </button>
          <button
            type="button"
            onClick={handleConfirm}
            disabled={isLoading}
            className={`${BUTTON_BASE} ${styles.confirm}`}
          >
            {isLoading && <Loader2 size={15} className="animate-spin" aria-hidden="true" />}
            {isLoading ? 'Working…' : confirmLabel}
          </button>
        </div>
      </div>
    </div>
  )
}

export default ConfirmDialog
