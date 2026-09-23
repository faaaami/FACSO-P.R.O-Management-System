function SectionCard({ title, icon: Icon, subtitle, badge, action, className = '', children }) {
  return (
    <div className={`bg-white rounded-[12px] border border-[#E2E4E9] shadow-[0_1px_3px_rgba(28,31,38,0.06)] p-6 ${className}`}>
      <div className="flex items-start justify-between gap-4 mb-5">
        <div className="min-w-0 flex items-start gap-3">
          {Icon && (
            <div className="flex h-9 w-9 shrink-0 items-center justify-center rounded-[8px] bg-[#F7F8FA] border border-[#E2E4E9] text-[#16181D]">
              <Icon size={18} strokeWidth={1.75} className="text-[#16181D]" aria-hidden="true" />
            </div>
          )}
          <div className="min-w-0">
            <div className="flex items-center gap-2.5 flex-wrap">
              <h2 className="text-base font-semibold text-[#16181D] tracking-tight">
                {title}
              </h2>
              {badge}
            </div>
            {subtitle && (
              <p className="mt-0.5 text-xs text-[#6B7280]">{subtitle}</p>
            )}
          </div>
        </div>
        {action && <div className="shrink-0">{action}</div>}
      </div>
      <div>{children}</div>
    </div>
  )
}

export default SectionCard