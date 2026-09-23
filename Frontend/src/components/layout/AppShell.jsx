import { useState } from 'react'
import { Outlet } from 'react-router-dom'
import Sidebar from './Sidebar'
import Header from './Header'

function AppShell() {
  const [isMobileMenuOpen, setIsMobileMenuOpen] = useState(false)

  return (
    <div className="min-h-screen bg-[#F7F8FA] flex">
      {/* Desktop Fixed Left Sidebar: 260px */}
      <aside className="hidden lg:flex w-[260px] flex-col shrink-0 border-r border-[#E2E4E9] bg-white h-screen sticky top-0 z-30">
        <Sidebar />
      </aside>

      {/* Mobile Drawer & Backdrop */}
      {isMobileMenuOpen && (
        <div className="fixed inset-0 z-40 lg:hidden" role="dialog" aria-modal="true">
          {/* Backdrop */}
          <div
            className="fixed inset-0 bg-slate-900/40 transition-opacity"
            onClick={() => setIsMobileMenuOpen(false)}
            aria-hidden="true"
          />

          {/* Drawer Sidebar */}
          <aside className="fixed inset-y-0 left-0 z-50 w-[260px] bg-white border-r border-[#E2E4E9] flex flex-col shadow-xl">
            <Sidebar
              isMobile
              onClose={() => setIsMobileMenuOpen(false)}
              onItemClick={() => setIsMobileMenuOpen(false)}
            />
          </aside>
        </div>
      )}

      {/* Main Content Area */}
      <div className="flex-1 flex flex-col min-w-0 min-h-screen">
        <Header onOpenMobileMenu={() => setIsMobileMenuOpen(true)} />

        <main className="flex-1 p-4 sm:p-6 lg:p-8 bg-[#F7F8FA] overflow-x-hidden">
          <div className="max-w-[1600px] mx-auto w-full">
            <Outlet />
          </div>
        </main>
      </div>
    </div>
  )
}

export default AppShell
