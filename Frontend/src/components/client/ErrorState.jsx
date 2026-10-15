import { AlertCircle, RotateCcw } from 'lucide-react'

function ErrorState({ message, onRetry }) {
  return (
    <div
      role="alert"
      className="flex flex-col items-center justify-center rounded-[10px] border border-red-200 bg-red-50/60 px-6 py-6 text-center"
    >
      <div className="mb-2 flex h-8 w-8 items-center justify-center rounded-full bg-red-100 text-[#DC2626]">
        <AlertCircle size={16} strokeWidth={2} aria-hidden="true" />
      </div>
      <p className="text-sm font-medium text-[#DC2626]">{message}</p>
      {onRetry && (
        <button
          type="button"
          onClick={onRetry}
          className="mt-3.5 inline-flex items-center gap-1.5 rounded-[10px] bg-[#1C1F26] px-3.5 py-1.5 text-xs font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] transition duration-150 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
        >
          <RotateCcw size={13} strokeWidth={2} aria-hidden="true" />
          <span>Try again</span>
        </button>
      )}
    </div>
  )
}

export default ErrorState