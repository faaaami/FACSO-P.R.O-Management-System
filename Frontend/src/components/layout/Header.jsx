import { useState, useRef, useEffect } from 'react'
import { useNavigate, Link, useLocation } from 'react-router-dom'
import {
  Menu,
  Bell,
  ChevronDown,
  Building2,
  LogOut,
  Settings,
  User as UserIcon,
} from 'lucide-react'
import { useAuth } from '../../auth/AuthContext'
import GlobalSearch from './GlobalSearch'

function Header({ onOpenMobileMenu, isMobileMenuOpen = false, mobileMenuId }) {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [isDropdownOpen, setIsDropdownOpen] = useState(false)
  const dropdownRef = useRef(null)

  useEffect(() => {
    setIsDropdownOpen(false)
  }, [location.pathname])

  useEffect(() => {
    function handleClickOutside(event) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target)) {
        setIsDropdownOpen(false)
      }
    }
    document.addEventListener('mousedown', handleClickOutside)
    return () => {
      document.removeEventListener('mousedown', handleClickOutside)
    }
  }, [])

  function handleSignOut() {
    setIsDropdownOpen(false)
    logout()
    navigate('/login')
  }

  const userInitial = (user?.fullName?.[0] || user?.email?.[0] || 'U').toUpperCase()
  const userRole = user?.role || 'Staff'
  const userDisplay = user?.fullName || user?.name || user?.email?.split('@')[0] || 'Operations Admin'

  return (
    <header className="h-16 bg-white border-b border-[#E2E4E9] px-4 sm:px-6 lg:px-8 flex items-center justify-between gap-4 sticky top-0 z-20">
      {/* Left Area: Mobile Menu Button & Mobile Branding */}
      <div className="flex items-center gap-3">
        <button
          type="button"
          onClick={onOpenMobileMenu}
          aria-label="Open navigation menu"
          aria-expanded={isMobileMenuOpen}
          aria-controls={mobileMenuId}
          className="lg:hidden rounded-[8px] p-2 text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] cursor-pointer"
        >
          <Menu className="h-5 w-5" />
        </button>

        {/* Mobile-only brand identifier */}
        <div className="flex lg:hidden items-center gap-2">
          <div className="flex h-7 w-7 items-center justify-center rounded-[6px] bg-[#1C1F26]">
            <Building2 className="h-3.5 w-3.5 text-[#0F9D74]" />
          </div>
          <span className="font-bold text-sm tracking-tight text-[#16181D]">
            FASCO <span className="text-[#0F9D74] text-xs">PRO</span>
          </span>
        </div>
      </div>

      {/* Middle Area: Role-scoped search (Agent: client lookup + jump to.
          Client/Admin: jump to only. See GlobalSearch.) */}
      <GlobalSearch />

      {/* Right Area: Notifications & User Profile */}
      <div className="flex items-center gap-3 sm:gap-4">
        {/* Notifications */}
        <Link
          to="/notifications"
          aria-label="View notifications"
          className="relative rounded-[8px] p-2 text-[#6B7280] hover:bg-gray-100 hover:text-[#16181D] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150"
        >
          <Bell className="h-5 w-5" />
          <span className="absolute top-1.5 right-1.5 h-2 w-2 rounded-full bg-[#0F9D74] ring-2 ring-white" />
        </Link>

        {/* Divider */}
        <div className="h-6 w-px bg-[#E2E4E9]" />

        {/* User Profile & Dropdown */}
        <div className="relative" ref={dropdownRef}>
          <button
            type="button"
            onClick={() => setIsDropdownOpen(!isDropdownOpen)}
            aria-expanded={isDropdownOpen}
            aria-haspopup="true"
            className="flex items-center gap-2.5 rounded-[10px] p-1.5 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
          >
            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-[#1C1F26] text-white text-xs font-semibold shrink-0">
              {userInitial}
            </div>
            <div className="hidden md:block text-left">
              <p className="text-sm font-semibold text-[#16181D] leading-tight truncate max-w-[140px]">
                {userDisplay}
              </p>
              <p className="text-[11px] font-medium text-[#6B7280] leading-none mt-0.5 capitalize">
                {userRole}
              </p>
            </div>
            <ChevronDown className="h-4 w-4 text-[#9CA3AF] hidden sm:block" />
          </button>

          {/* User Dropdown Menu */}
          {isDropdownOpen && (
            <div className="absolute right-0 top-full mt-2 w-56 rounded-[12px] bg-white border border-[#E2E4E9] shadow-[0_8px_24px_rgba(28,31,38,0.10)] py-1.5 z-50 animate-in fade-in duration-100">
              <div className="px-4 py-2.5 border-b border-[#E2E4E9]">
                <p className="text-xs text-[#6B7280]">Signed in as</p>
                <p className="text-sm font-semibold text-[#16181D] truncate">
                  {user?.email || 'Operations Admin'}
                </p>
                <span className="inline-block mt-1 rounded-[6px] bg-[rgba(15,157,116,0.08)] px-2 py-0.5 text-[10px] font-semibold text-[#0F9D74] border border-[#0F9D74]/20">
                  {userRole}
                </span>
              </div>

              <div className="py-1">
                <Link
                  to="/settings"
                  onClick={() => setIsDropdownOpen(false)}
                  className="flex items-center gap-2.5 px-4 py-2 text-sm text-[#16181D] hover:bg-gray-50 transition-colors"
                >
                  <Settings className="h-4 w-4 text-[#6B7280]" />
                  <span>Settings</span>
                </Link>
                {user?.role !== 'Agent' && (
                  <Link
                    to={user?.role === 'Client' ? '/company-profile' : '/staff'}
                    onClick={() => setIsDropdownOpen(false)}
                    className="flex items-center gap-2.5 px-4 py-2 text-sm text-[#16181D] hover:bg-gray-50 transition-colors"
                  >
                    <UserIcon className="h-4 w-4 text-[#6B7280]" />
                    <span>Profile</span>
                  </Link>
                )}
              </div>

              <div className="border-t border-[#E2E4E9] pt-1">
                <button
                  type="button"
                  onClick={handleSignOut}
                  className="flex w-full items-center gap-2.5 px-4 py-2 text-sm text-[#DC2626] hover:bg-red-50 transition-colors cursor-pointer"
                >
                  <LogOut className="h-4 w-4 text-[#DC2626]" />
                  <span>Sign out</span>
                </button>
              </div>
            </div>
          )}
        </div>
      </div>
    </header>
  )
}

export default Header
