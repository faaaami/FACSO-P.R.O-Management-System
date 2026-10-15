function InfoRow({ label, value, icon: Icon, mono }) {
  return (
    <div className="flex items-center justify-between gap-4">
      <div className="flex min-w-0 items-center gap-2">
        {Icon && <Icon size={14} className="shrink-0 text-[#9CA3AF]" aria-hidden="true" />}
        <span className="shrink-0 text-xs font-medium text-[#6B7280] uppercase tracking-wider">{label}</span>
      </div>
      <span
        className={`min-w-0 break-words text-right text-sm font-semibold text-[#16181D] ${
          mono ? 'font-mono' : ''
        }`}
      >
        {value ?? 'N/A'}
      </span>
    </div>
  )
}

export default InfoRow