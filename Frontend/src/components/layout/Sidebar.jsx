import { NavLink } from 'react-router-dom'
import { Building2, X } from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import { ROLE_NAVIGATION } from './navigation'

function Sidebar({ isMobile = false, onClose, onItemClick }) {
  const { user } = useAuth()
  const items = ROLE_NAVIGATION[user?.role] ?? []
  return (
    <div className="flex h-full flex-col bg-white">
      {/* Brand Header */}
      <div className="flex h-16 items-center justify-between px-6 border-b border-[#E2E4E9] shrink-0">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-[8px] bg-[#1C1F26] shadow-[0_1px_3px_rgba(28,31,38,0.06)]">
            <Building2 className="h-4 w-4 text-[#0F9D74]" />
          </div>
          <div className="flex items-center gap-1.5">
            <span className="text-base font-bold tracking-tight text-[#16181D]">
              FASCO
            </span>
            <span className="rounded-[6px] bg-[rgba(15,157,116,0.08)] px-1.5 py-0.5 text-[10px] font-bold tracking-wider text-[#0F9D74] border border-[#0F9D74]/20">
              PRO
            </span>
          </div>
        </div>

        {isMobile && (
          <button
            type="button"
            onClick={onClose}
            aria-label="Close navigation drawer"
            className="rounded-[8px] p-1.5 text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
          >
            <X className="h-5 w-5" />
          </button>
        )}
      </div>

      {/* Navigation List */}
      <nav className="flex-1 overflow-y-auto py-3 space-y-0.5">
        {items.map((item) => {
          const Icon = item.icon
          return (
            <NavLink
              key={item.path}
              to={item.path}
              end={item.exact}
              onClick={onItemClick}
              className={({ isActive }) =>
                `flex items-center gap-3 px-5 py-2.5 text-sm transition-colors duration-150 border-l-[3px] ${
                  isActive
                    ? 'bg-[rgba(15,157,116,0.08)] text-[#0F9D74] border-[#0F9D74] font-semibold'
                    : 'text-[#6B7280] border-transparent hover:bg-gray-50 hover:text-[#16181D] font-medium'
                }`
              }
            >
              <Icon className="h-4 w-4 shrink-0" />
              <span>{item.name}</span>
            </NavLink>
          )
        })}
      </nav>

      {/* Footer / Context */}
      <div className="border-t border-[#E2E4E9] p-4 px-6 shrink-0">
        <p className="text-xs font-semibold text-[#16181D]">FASCO PRO Service</p>
        <p className="text-[11px] text-[#6B7280]">UAE Government Portal</p>
      </div>
    </div>
  )
}

export default Sidebar
