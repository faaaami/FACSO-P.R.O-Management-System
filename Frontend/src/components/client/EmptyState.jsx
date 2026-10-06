import { Inbox } from 'lucide-react'

function EmptyState({ message, icon, description }) {
  const Icon = icon || Inbox
  return (
    <div className="flex flex-col items-center justify-center rounded-[10px] border border-dashed border-[#E2E4E9] bg-[#F7F8FA]/80 px-6 py-8 text-center">
      <div className="mb-2.5 flex h-9 w-9 items-center justify-center rounded-[8px] bg-white border border-[#E2E4E9] text-[#9CA3AF] shadow-[0_1px_2px_rgba(28,31,38,0.04)]">
        <Icon size={18} strokeWidth={1.75} aria-hidden="true" />
      </div>
      <p className="text-sm font-medium text-[#16181D]">{message}</p>
      {description && (
        <p className="mt-1 max-w-sm text-xs text-[#6B7280]">{description}</p>
      )}
    </div>
  )
}

export default EmptyState