import { useId } from 'react'
import { Loader2, X } from 'lucide-react'
import { useFocusTrap } from '../../hooks/useFocusTrap'

/**
 * Small form and layout primitives shared by the service catalogue, counter
 * and job screens, styled per DESIGN.md (10px controls, hairline borders,
 * #0F9D74 focus ring).
 */

const BUTTON_VARIANTS = {
  primary: 'bg-[#1C1F26] text-white hover:bg-[#101319]',
  accent: 'bg-[#0F9D74] text-white hover:bg-[#0B8160]',
  secondary: 'border border-[#E2E4E9] bg-white text-[#16181D] hover:bg-[#F7F8FA]',
  danger: 'border border-[#DC2626]/30 bg-white text-[#DC2626] hover:bg-red-50',
  ghost: 'text-[#16181D] hover:bg-[#F7F8FA]',
}

export function Button({ variant = 'secondary', size = 'md', loading = false, icon: Icon, children, className = '', ...props }) {
  const sizing = size === 'sm' ? 'px-2.5 py-1.5 text-xs' : 'px-4 py-2 text-sm'
  return (
    <button
      type="button"
      {...props}
      disabled={props.disabled || loading}
      className={`inline-flex cursor-pointer items-center justify-center gap-1.5 rounded-[10px] font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.25)] disabled:cursor-not-allowed disabled:opacity-50 ${sizing} ${BUTTON_VARIANTS[variant]} ${className}`}
    >
      {loading ? (
        <Loader2 size={14} className="animate-spin" aria-hidden="true" />
      ) : (
        Icon && <Icon size={14} strokeWidth={2} aria-hidden="true" />
      )}
      {children}
    </button>
  )
}

const CONTROL =
  'w-full rounded-[10px] border border-[#E2E4E9] bg-white px-3 py-2 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] disabled:bg-[#F7F8FA] disabled:text-[#6B7280]'

export function Field({ label, hint, error, required, children, className = '' }) {
  const id = useId()
  return (
    <div className={className}>
      {label && (
        <label htmlFor={id} className="mb-1 block text-xs font-semibold text-[#16181D]">
          {label}
          {required && <span className="text-[#DC2626]"> *</span>}
        </label>
      )}
      {typeof children === 'function' ? children(id) : children}
      {hint && !error && <p className="mt-1 text-xs text-[#6B7280]">{hint}</p>}
      {error && <p className="mt-1 text-xs text-[#DC2626]">{error}</p>}
    </div>
  )
}

export function Input({ className = '', ...props }) {
  return <input {...props} className={`${CONTROL} ${className}`} />
}

export function Select({ className = '', children, ...props }) {
  return (
    <select {...props} className={`${CONTROL} ${className}`}>
      {children}
    </select>
  )
}

export function TextArea({ className = '', ...props }) {
  return <textarea rows={3} {...props} className={`${CONTROL} ${className}`} />
}

/** Centered modal dialog with a focus trap. */
export function Modal({ title, subtitle, onClose, size = 'md', footer, children }) {
  const titleId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const width = size === 'lg' ? 'max-w-3xl' : size === 'sm' ? 'max-w-sm' : 'max-w-xl'

  return (
    <div className="fixed inset-0 z-50 flex items-start justify-center overflow-y-auto p-4 sm:p-8">
      <div className="fixed inset-0 bg-slate-900/40" onClick={onClose} aria-hidden="true" />
      <div
        ref={panelRef}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}
        className={`relative z-10 w-full ${width} rounded-[12px] border border-[#E2E4E9] bg-white shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none`}
      >
        <div className="flex items-start justify-between gap-4 border-b border-[#E2E4E9] px-6 py-4">
          <div className="min-w-0">
            <h2 id={titleId} className="text-base font-semibold tracking-tight text-[#16181D]">{title}</h2>
            {subtitle && <p className="mt-0.5 text-xs text-[#6B7280]">{subtitle}</p>}
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="cursor-pointer rounded-[8px] p-1.5 text-[#6B7280] transition duration-150 hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74]"
          >
            <X className="h-5 w-5" aria-hidden="true" />
          </button>
        </div>
        <div className="px-6 py-5">{children}</div>
        {footer && <div className="flex flex-wrap justify-end gap-2 border-t border-[#E2E4E9] px-6 py-4">{footer}</div>}
      </div>
    </div>
  )
}

export function KpiTile({ label, value, tone = 'neutral', active, onClick }) {
  const tones = {
    neutral: 'text-[#16181D]',
    success: 'text-[#0F9D74]',
    warning: 'text-[#D97706]',
    danger: 'text-[#DC2626]',
  }
  const Tag = onClick ? 'button' : 'div'
  return (
    <Tag
      type={onClick ? 'button' : undefined}
      onClick={onClick}
      className={`rounded-[12px] border bg-white p-4 text-left shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 ${
        active ? 'border-[#0F9D74] ring-2 ring-[rgba(15,157,116,0.15)]' : 'border-[#E2E4E9]'
      } ${onClick ? 'cursor-pointer hover:border-[#0F9D74]/40' : ''}`}
    >
      <p className="text-xs font-medium text-[#6B7280]">{label}</p>
      <p className={`mt-1 text-2xl font-bold tabular-nums tracking-tight ${tones[tone]}`}>{value ?? '—'}</p>
    </Tag>
  )
}

export function Chip({ children, tone = 'neutral' }) {
  const tones = {
    neutral: 'border-[#E2E4E9] bg-[#F7F8FA] text-[#6B7280]',
    accent: 'border-[#0F9D74]/20 bg-[rgba(15,157,116,0.08)] text-[#0F9D74]',
    warning: 'border-[#D97706]/20 bg-amber-50 text-[#D97706]',
    danger: 'border-[#DC2626]/20 bg-red-50 text-[#DC2626]',
  }
  return (
    <span className={`inline-flex items-center gap-1 rounded-[6px] border px-2 py-0.5 text-xs font-medium ${tones[tone]}`}>
      {children}
    </span>
  )
}
