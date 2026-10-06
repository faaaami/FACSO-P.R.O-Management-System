import { useRef } from 'react'
import { BILLING_TABS } from './billingDisplay'

/**
 * The four billing tabs, and the tablist that switches between them.
 *
 * They are TABS, not separate pages, and the choice is not cosmetic:
 *
 *   - ONE SHARED COMPANY FILTER SITS ABOVE THIS TABLIST, so switching tabs must not
 *     reset it. A company selected on Retainer is still the selected company on
 *     Payments, because it is the same selection rendered once.
 *   - EACH TAB KEEPS ITS OWN PAGE AND STATUS. Switching back to a tab must return to
 *     the page the reader was on, so the page number is owned by the page component
 *     and passed in — a tablist that reset its own state on activation would be a
 *     fourth place for pagination state to live.
 *   - THERE IS NO DEEP LINK TO ANY TAB. There is no ?tab= parameter and no
 *     /invoices/:kind route, because no Admin route accepts one and inventing a
 *     query parameter here would be a control nothing reads.
 *
 * The tab table itself lives in billingDisplay.js so that this file exports only a
 * component: see the note there for why.
 *
 * FULL KEYBOARD SUPPORT, because a tablist that only responds to clicks is unusable
 * without a mouse: Left/Right move, Home/End jump to the ends, and a ROVING tabIndex
 * means Tab leaves the tablist rather than walking through all four tabs. Selecting
 * with an arrow key follows focus, which is the behaviour the ARIA pattern requires.
 */

/** The tab a key maps to, or null when the key is not a tab-navigation key. */
function nextTabIndex(currentIndex, key) {
  const last = BILLING_TABS.length - 1

  if (key === 'ArrowRight') return (currentIndex + 1) % BILLING_TABS.length
  if (key === 'ArrowLeft') return (currentIndex - 1 + BILLING_TABS.length) % BILLING_TABS.length
  if (key === 'Home') return 0
  if (key === 'End') return last
  return null
}

function AdminBillingTabs({ activeTab, onTabChange, tabPanelId, className = '' }) {
  const tabRefs = useRef([])
  const currentIndex = Math.max(
    0,
    BILLING_TABS.findIndex((tab) => tab.key === activeTab),
  )

  function handleKeyDown(event) {
    const target = nextTabIndex(currentIndex, event.key)
    if (target == null) return

    event.preventDefault()
    onTabChange(BILLING_TABS[target].key)
    tabRefs.current[target]?.focus()
  }

  return (
    <div
      role="tablist"
      aria-label="Billing sections"
      onKeyDown={handleKeyDown}
      className={`flex gap-1 overflow-x-auto border-b border-[#E2E4E9] ${className}`}
    >
      {BILLING_TABS.map((tab, index) => {
        const Icon = tab.icon
        const isActive = tab.key === activeTab

        return (
          <button
            key={tab.key}
            ref={(node) => {
              tabRefs.current[index] = node
            }}
            type="button"
            role="tab"
            id={`admin-billing-tab-${tab.key}`}
            aria-selected={isActive}
            aria-controls={tabPanelId}
            tabIndex={isActive ? 0 : -1}
            onClick={() => onTabChange(tab.key)}
            className={`inline-flex shrink-0 cursor-pointer items-center gap-1.5 whitespace-nowrap border-b-2 px-3.5 py-2.5 text-sm font-semibold transition duration-150 focus:outline-none focus:ring-2 focus:ring-inset focus:ring-[rgba(15,157,116,0.15)] ${
              isActive
                ? 'border-[#0F9D74] text-[#0F9D74]'
                : 'border-transparent text-[#6B7280] hover:border-[#E2E4E9] hover:text-[#16181D]'
            }`}
          >
            <Icon size={15} strokeWidth={1.75} aria-hidden="true" />
            {tab.label}
          </button>
        )
      })}
    </div>
  )
}

export default AdminBillingTabs
