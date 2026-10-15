import { useState } from 'react'
import { AlertTriangle, CreditCard } from 'lucide-react'
import { toast } from 'sonner'
import { useQueryClient } from '@tanstack/react-query'
import { useCreateClientPaymentOrder } from '../../../hooks/client/useCreateClientPaymentOrder'

const BILLING_QUERY_KEYS = [
  ['client', 'retainer-invoices'],
  ['client', 'service-fee-invoices'],
  ['client', 'gov-fee-disbursements'],
  ['client', 'payment-history'],
]

const CHECKOUT_SCRIPT_URL = 'https://checkout.razorpay.com/v1/checkout.js'

let scriptPromise = null

function loadRazorpayScript() {
  if (typeof window !== 'undefined' && window.Razorpay) {
    return Promise.resolve(window.Razorpay)
  }
  if (scriptPromise) return scriptPromise

  scriptPromise = new Promise((resolve, reject) => {
    const existing = document.querySelector(`script[src="${CHECKOUT_SCRIPT_URL}"]`)
    if (existing) {
      const onLoad = () => {
        cleanup()
        resolve(window.Razorpay)
      }
      const onError = () => {
        cleanup()
        reject(new Error('failed'))
      }
      const cleanup = () => {
        existing.removeEventListener('load', onLoad)
        existing.removeEventListener('error', onError)
      }
      existing.addEventListener('load', onLoad)
      existing.addEventListener('error', onError)
      return
    }

    const script = document.createElement('script')
    script.src = CHECKOUT_SCRIPT_URL
    script.async = true
    script.onload = () => resolve(window.Razorpay)
    script.onerror = () => reject(new Error('failed'))
    document.body.appendChild(script)
  })

  return scriptPromise
}

function orderErrorMessage(err) {
  switch (err?.response?.status) {
    case 403:
      return 'You are not authorised to pay this invoice.'
    case 404:
      return 'The invoice could not be found or is no longer available.'
    case 409:
      return 'This invoice can no longer be paid. It may already have been settled or voided.'
    case 400:
      return 'This invoice cannot be paid right now.'
    default:
      return 'We could not start the payment. Please try again.'
  }
}

/**
 * Starts a Razorpay checkout for one invoice.
 *
 * The component deliberately does NOT decide what happens after the gateway
 * confirms a payment. Settlement is asynchronous (the provider's webhook is
 * the authority), so instead of closing the drawer and leaving the client
 * guessing, `onPaymentSubmitted` hands control to the parent, which owns the
 * confirming state and keeps watching the invoice until it settles.
 */
function PaymentCheckout({ kind, item, label, onPaymentSubmitted }) {
  const createOrder = useCreateClientPaymentOrder()
  const queryClient = useQueryClient()
  const [error, setError] = useState(null)
  const [opening, setOpening] = useState(false)
  // Tracks the whole lifetime of the gateway modal, not just the synchronous
  // `checkout.open()` call. `open()` returns immediately while the modal is
  // still on screen, so keying the guard on `opening` alone re-enabled the
  // button for the entire window the customer was typing their card.
  const [modalOpen, setModalOpen] = useState(false)

  const invoiceTypeToken = kind === 'retainer' ? 'Retainer' : 'ServiceFee'

  const refreshBilling = () => {
    BILLING_QUERY_KEYS.forEach((queryKey) => {
      queryClient.invalidateQueries({ queryKey })
    })
  }

  const closeModal = () => {
    setModalOpen(false)
    setOpening(false)
  }

  const openCheckout = async (order) => {
    try {
      const RazorpayConstructor = await loadRazorpayScript()
      setOpening(true)

      const options = {
        key: order.razorpayKeyId,
        order_id: order.razorpayOrderId,
        amount: order.razorpayAmount,
        currency: order.currency || item?.currency || 'AED',
        name: 'FASCO PRO Service',
        description: `${label}${item?.invoiceNumber ? ` — ${item.invoiceNumber}` : ''}`,
        theme: { color: '#0F9D74' },
        modal: {
          // Dismissed without paying: the order stays open and reusable, so
          // the customer can simply press Pay again.
          ondismiss: () => {
            closeModal()
            toast.info(
              'Payment was not completed. Your invoice status will only change once the payment is confirmed.',
            )
          },
        },
        handler: (response) => {
          // The provider accepted the payment. Settlement still happens via
          // webhook, so hand over to the parent's confirming state rather than
          // claiming success. response is intentionally unused: the server
          // re-verifies the signature, and the browser cannot be trusted to
          // decide that money arrived.
          closeModal()
          refreshBilling()
          onPaymentSubmitted?.(order, response)
        },
      }

      const checkout = new RazorpayConstructor(options)
      checkout.on('payment.failed', () => {
        closeModal()
        toast.error('The payment was not completed. Please try again or contact the PRO team.')
      })
      setModalOpen(true)
      checkout.open()
    } catch {
      closeModal()
      setError('The payment gateway could not be reached. Please try again in a moment.')
      toast.error('The payment gateway could not be reached. Please retry.')
    }
  }

  const handlePay = () => {
    setError(null)
    createOrder.mutate(
      { invoiceType: invoiceTypeToken, invoiceId: item.id },
      {
        onSuccess: (order) => openCheckout(order),
        onError: (err) => {
          closeModal()
          const message = orderErrorMessage(err)
          setError(message)
          if (err?.response?.status === 409) refreshBilling()
        },
      },
    )
  }

  const busy = createOrder.isPending || opening || modalOpen

  return (
    <div className="space-y-2.5">
      <button
        type="button"
        disabled={busy}
        onClick={handlePay}
        className="inline-flex w-full items-center justify-center gap-2 rounded-[10px] bg-[#0F9D74] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#0B7D5D] disabled:opacity-50 disabled:cursor-not-allowed focus:outline-none focus:ring-2 focus:ring-[#0F9D74] transition duration-150 cursor-pointer"
      >
        <CreditCard size={16} strokeWidth={2} aria-hidden="true" />
        {createOrder.isPending
          ? 'Preparing payment…'
          : opening
            ? 'Opening secure payment…'
            : modalOpen
              ? 'Waiting for payment…'
              : 'Pay Invoice'}
      </button>

      {error && (
        <div className="flex items-start gap-2 rounded-[8px] bg-red-50 p-3 text-xs font-medium text-[#DC2626] border border-red-200">
          <AlertTriangle size={14} className="shrink-0 mt-0.5" aria-hidden="true" />
          <span>{error}</span>
        </div>
      )}
    </div>
  )
}

export default PaymentCheckout