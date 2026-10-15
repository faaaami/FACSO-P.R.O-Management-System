import { useState } from 'react'
import { AlertCircle, Building2, CheckCircle2, Eye, EyeOff, Lock } from 'lucide-react'

/** Branded full-page card shared by the sign-in and account-link pages. */
export function AuthLayout({ title, subtitle, children }) {
  return (
    <div className="min-h-screen bg-[#F7F8FA] flex flex-col justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="sm:mx-auto sm:w-full sm:max-w-md">
        <div className="flex flex-col items-center text-center mb-8">
          <div className="flex items-center gap-3 mb-2">
            <div className="flex h-11 w-11 items-center justify-center rounded-[10px] bg-[#1C1F26] shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
              <Building2 className="h-5 w-5 text-[#0F9D74]" />
            </div>
            <div className="flex items-center gap-2">
              <span className="text-2xl font-bold tracking-tight text-[#16181D]">FASCO</span>
              <span className="rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-xs font-semibold tracking-wide text-[#0F9D74] border border-[#0F9D74]/20">
                PRO Service
              </span>
            </div>
          </div>
          <p className="text-xs font-medium text-[#6B7280] tracking-wider uppercase">
            UAE Government Relations & Corporate PRO Management
          </p>
        </div>

        <div className="bg-white rounded-[12px] border border-[#E2E4E9] shadow-[0_1px_3px_rgba(28,31,38,0.06)] p-7 sm:p-8">
          <div className="mb-6 text-center">
            <h1 className="text-xl font-semibold tracking-tight text-[#16181D]">{title}</h1>
            {subtitle && <p className="mt-1.5 text-sm text-[#6B7280]">{subtitle}</p>}
          </div>
          {children}
        </div>

        <div className="mt-8 text-center text-xs text-[#9CA3AF]">
          &copy; {new Date().getFullYear()} FASCO PRO Service. All rights reserved.
        </div>
      </div>
    </div>
  )
}

export function AuthAlert({ tone = 'error', children }) {
  const isError = tone === 'error'
  const Icon = isError ? AlertCircle : CheckCircle2
  return (
    <div
      role={isError ? 'alert' : 'status'}
      className={`mb-5 flex items-start gap-2.5 rounded-[10px] p-3 text-sm border ${
        isError
          ? 'bg-red-50 text-[#DC2626] border-red-200'
          : 'bg-[rgba(15,157,116,0.06)] text-[#0B7A5A] border-[#0F9D74]/20'
      }`}
    >
      <Icon className="h-4 w-4 shrink-0 mt-0.5" />
      <div className="leading-snug font-medium text-xs sm:text-sm">{children}</div>
    </div>
  )
}

export const authInputClass =
  'block w-full rounded-[10px] border border-[#E2E4E9] bg-white pl-10 pr-10 py-2.5 text-sm text-[#16181D] placeholder:text-[#9CA3AF] focus:border-[#0F9D74] focus:ring-2 focus:ring-[rgba(15,157,116,0.15)] focus:outline-none disabled:bg-gray-50 disabled:text-[#9CA3AF] disabled:cursor-not-allowed transition duration-150'

export const authButtonClass =
  'w-full flex items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer'

export const authLinkClass =
  'font-medium text-[#0F9D74] hover:text-[#0B7A5A] focus:outline-none focus:underline'

export function PasswordInput({ id, label, value, onChange, disabled, autoComplete = 'new-password' }) {
  const [visible, setVisible] = useState(false)
  return (
    <div>
      <label htmlFor={id} className="block text-sm font-medium text-[#16181D] mb-1.5">
        {label}
      </label>
      <div className="relative">
        <div className="pointer-events-none absolute inset-y-0 left-0 flex items-center pl-3.5 text-[#9CA3AF]">
          <Lock className="h-4 w-4" />
        </div>
        <input
          id={id}
          type={visible ? 'text' : 'password'}
          value={value}
          onChange={(event) => onChange(event.target.value)}
          autoComplete={autoComplete}
          disabled={disabled}
          required
          className={authInputClass}
        />
        <button
          type="button"
          onClick={() => setVisible(!visible)}
          disabled={disabled}
          aria-label={visible ? 'Hide password' : 'Show password'}
          className="absolute inset-y-0 right-0 flex items-center pr-3.5 text-[#9CA3AF] hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] transition-colors cursor-pointer disabled:cursor-not-allowed"
        >
          {visible ? <EyeOff className="h-4 w-4" /> : <Eye className="h-4 w-4" />}
        </button>
      </div>
    </div>
  )
}
