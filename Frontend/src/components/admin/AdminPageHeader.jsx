/**
 * Consistent page header for Admin Portal pages: title, optional subtitle, and
 * an optional right-hand action slot. Mirrors the AgentPageHeader layout while
 * keeping only the props the Admin pages need (no badge/meta yet).
 */
function AdminPageHeader({ title, subtitle, action }) {
  return (
    <div className="flex flex-col gap-4 border-b border-[#E2E4E9] pb-5 md:flex-row md:items-end md:justify-between">
      <div className="min-w-0">
        <h1 className="text-2xl font-bold tracking-tight text-[#16181D]">{title}</h1>
        {subtitle && <p className="mt-1.5 text-sm text-[#6B7280]">{subtitle}</p>}
      </div>
      {action && <div className="flex shrink-0 flex-wrap items-center gap-2">{action}</div>}
    </div>
  )
}

export default AdminPageHeader
