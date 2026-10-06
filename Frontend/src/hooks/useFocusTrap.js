import { useEffect, useRef } from 'react'

const FOCUSABLE_SELECTOR =
  'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'

// Stack of currently open modal surfaces. Only the most recently opened one
// responds to Escape/Tab, so overlapping surfaces (a drawer opened from another
// drawer) behave like a proper modal stack instead of closing together.
const activeSurfaces = []
let surfaceIdCounter = 0

/**
 * Traps keyboard focus inside a modal surface, closes it on Escape, locks
 * background scrolling, and moves focus in on open / back to the trigger on
 * close. Returns a ref to attach to the focusable container (the container
 * needs tabIndex={-1} so it can receive focus when it has no focusable
 * children).
 */
export function useFocusTrap({ isOpen, onClose }) {
  const containerRef = useRef(null)
  const previouslyFocusedRef = useRef(null)
  const hasCapturedRef = useRef(false)
  const surfaceIdRef = useRef(null)

  if (surfaceIdRef.current === null) {
    surfaceIdRef.current = ++surfaceIdCounter
  }

  // Capture the trigger element the first time the surface opens, before the
  // focus effect below moves focus into the container.
  useEffect(() => {
    if (isOpen && !hasCapturedRef.current) {
      previouslyFocusedRef.current = document.activeElement
      hasCapturedRef.current = true
    }
  }, [isOpen])

  // Register/unregister this surface in the modal stack.
  useEffect(() => {
    if (!isOpen) {
      return
    }
    const id = surfaceIdRef.current
    activeSurfaces.push(id)
    return () => {
      const index = activeSurfaces.indexOf(id)
      if (index !== -1) {
        activeSurfaces.splice(index, 1)
      }
    }
  }, [isOpen])

  // Escape closes the surface and Tab is trapped inside it while open, so the
  // aria-modal dialog is honest rather than decorative.
  useEffect(() => {
    if (!isOpen) {
      return
    }
    const id = surfaceIdRef.current

    function isTopmost() {
      return activeSurfaces[activeSurfaces.length - 1] === id
    }

    function handleKeyDown(event) {
      if (!isTopmost()) {
        return
      }

      if (event.key === 'Escape') {
        event.preventDefault()
        onClose?.()
        return
      }

      if (event.key !== 'Tab') {
        return
      }

      const container = containerRef.current
      if (!container) {
        return
      }

      const focusable = Array.from(container.querySelectorAll(FOCUSABLE_SELECTOR)).filter(
        (node) => node.offsetParent !== null,
      )

      if (focusable.length === 0) {
        event.preventDefault()
        container.focus()
        return
      }

      const first = focusable[0]
      const last = focusable[focusable.length - 1]
      const active = document.activeElement

      if (event.shiftKey && (active === first || !container.contains(active))) {
        event.preventDefault()
        last.focus()
      } else if (!event.shiftKey && active === last) {
        event.preventDefault()
        first.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [isOpen, onClose])

  // Lock background scrolling while the surface is open.
  useEffect(() => {
    if (!isOpen) {
      return
    }
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      document.body.style.overflow = previousOverflow
    }
  }, [isOpen])

  // Move focus into the surface on open, and back to the trigger on close.
  useEffect(() => {
    if (!isOpen) {
      return
    }

    const container = containerRef.current
    if (container) {
      const firstFocusable = container.querySelector(FOCUSABLE_SELECTOR)
      ;(firstFocusable ?? container).focus()
    }

    return () => {
      const trigger = previouslyFocusedRef.current
      if (trigger && typeof trigger.focus === 'function') {
        trigger.focus()
      }
      previouslyFocusedRef.current = null
      hasCapturedRef.current = false
    }
  }, [isOpen])

  return containerRef
}

export default useFocusTrap
