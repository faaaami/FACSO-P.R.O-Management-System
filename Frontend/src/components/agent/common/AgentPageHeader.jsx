/**
 * Consistent page header for Agent Portal pages: title, optional description,
 * and an optional right-hand actions slot. Follows DESIGN.md — 2xl bold title
 * in primary ink, muted description, hairline bottom border.
 */
function AgentPageHeader({ title, description, badge, actions, meta }) {
  return (
    <div className="flex flex-col gap-4 border-b border-[#E2E4E9] pb-5 md:flex-row md:items-end md:justify-between">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2.5">
          <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">{title}</h1>
          {badge}
        </div>
        {description && <p className="mt-1.5 text-sm text-[#6B7280]">{description}</p>}
        {meta && <div className="mt-2.5">{meta}</div>}
      </div>
      {actions && <div className="flex shrink-0 flex-wrap items-center gap-2">{actions}</div>}
    </div>
  )
}

export default AgentPageHeader
