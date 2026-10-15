import { cloneElement, isValidElement } from 'react'
import { AlertCircle } from 'lucide-react'

const inputBase =
  'block w-full rounded-[10px] border bg-white px-3.5 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:outline-none focus:ring-2 transition duration-150 disabled:bg-gray-50 disabled:text-[#9CA3AF] disabled:cursor-not-allowed'

export const inputClass = `${inputBase} border-[#E2E4E9] focus:border-[#0F9D74] focus:ring-[rgba(15,157,116,0.15)]`

export const inputErrorClass = `${inputBase} border-[#DC2626] ring-2 ring-red-100 focus:border-[#DC2626] focus:ring-[rgba(220,38,38,0.15)]`

function FormField({ label, htmlFor, error, children }) {
  const errorId = htmlFor ? `${htmlFor}-error` : undefined

  // Associate the error message with the control so screen readers announce it
  // with the field, and mark the control invalid for assistive tech.
  let control = children
  if (error && isValidElement(children)) {
    const existingDescribedBy = children.props['aria-describedby']
    control = cloneElement(children, {
      'aria-invalid': true,
      'aria-describedby': [existingDescribedBy, errorId].filter(Boolean).join(' ') || undefined,
    })
  }

  return (
    <div>
      <label htmlFor={htmlFor} className="block text-sm font-medium text-[#16181D] mb-1.5">
        {label}
      </label>
      {control}
      {error && (
        <p
          id={errorId}
          role="alert"
          className="mt-1.5 flex items-center gap-1 text-xs font-medium text-[#DC2626]"
        >
          <AlertCircle size={12} strokeWidth={2} aria-hidden="true" />
          {error}
        </p>
      )}
    </div>
  )
}

export default FormField
