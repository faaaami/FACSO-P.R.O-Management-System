import { Eye, Wallet } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  MISSING_VALUE,
  dateTimeText,
  gatewayOrderText,
  linkedInvoiceReference,
  moneyParts,
  paymentInvoiceTypeText,
  paymentOrderStatusText,
  paymentOrderStatusTone,
  presentText,
  shortGuid,
} from './billingDisplay'

/**
 * Admin payment-order list. From PaymentOrderDto.
 *
 * ------------------------------------------------------------------
 * TWO NUMERIC ENUMS, LOOKED UP WITHOUT COERCION.
 * ------------------------------------------------------------------
 * PaymentOrderDto declares `InvoiceType` and `Status` as `int`, so unlike every
 * other Admin contract these two arrive as NUMBERS (1..2 and 1..6) and the global
 * JsonStringEnumConverter does not apply to them. The maps in billingDisplay are
 * therefore keyed numerically and are indexed directly. `String(status)` is never
 * called: that is the coercion that turns a number into a key that may not exist,
 * and it is exactly the mistake this module must not repeat.
 *
 * The consequence of a wire-type change would be a MISS and a visible raw value,
 * not a silently wrong label — which is the behaviour worth having.
 *
 * NO STATUS FILTER EXISTS ON THIS TAB, and that is a backend defect rather than a
 * design preference: the list endpoint accepts `status`, but the read repository
 * passes it as a string against an integer column, so PostgreSQL fails the
 * comparison and the request 500s. See useAdminPayments.js.
 *
 * THE GATEWAY ORDER ID IS NOT A GUID. It is a free-text string issued by Razorpay
 * and it is printed whole in a monospace face — shortening it would produce a
 * truncated value that still looks like a complete identifier, which is worse than
 * a long cell.
 */
function AmountCell({ amount, currency }) {
  const money = moneyParts(amount, currency)

  return (
    <div className="min-w-0">
      <p className="tabular-nums text-[#16181D]">{money.formatted}</p>
      <p className="mt-0.5 text-[11px] text-[#9CA3AF]">
        {money.hasCurrency ? `Currency: ${money.rawCurrency}` : 'Currency not recorded'}
      </p>
    </div>
  )
}

function LinkedInvoiceCell({ payment }) {
  const linked = linkedInvoiceReference(payment)

  if (!linked.present) {
    return <span className="text-[#9CA3AF]">No linked invoice id</span>
  }

  return (
    <div className="min-w-0">
      <p className="break-words text-[#16181D]">{linked.typeLabel ?? 'Invoice'}</p>
      <p className="mt-0.5 break-all font-mono text-[11px] text-[#6B7280]" title={String(linked.id)}>
        {shortGuid(linked.id)}
      </p>
    </div>
  )
}

function DetailsButton({ payment, onOpenDetails }) {
  return (
    <button
      type="button"
      onClick={() => onOpenDetails(payment.id)}
      aria-label={`View details for the ${paymentInvoiceTypeText(payment.invoiceType) ?? 'linked invoice'} payment order ${shortGuid(payment.id) ?? ''}`.trim()}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Eye size={13} strokeWidth={1.75} aria-hidden="true" />
      Details
    </button>
  )
}

function AdminPaymentOrderTable({ items, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[960px] border-collapse text-left">
          <caption className="sr-only">
            Payment orders, newest first. Each row shows the invoice type being
            paid, status, client company, the linked invoice id, amount with its
            stored currency, creation time and the gateway order reference, with a
            Details action that opens the full payment order.
          </caption>

          <thead>
            <tr className="border-b border-[#E2E4E9]">
              {[
                'Invoice type',
                'Status',
                'Client company',
                'Linked invoice',
                'Amount',
                'Created',
                'Gateway order',
              ].map((heading) => (
                <th
                  key={heading}
                  scope="col"
                  className="px-3 py-2.5 text-xs font-semibold uppercase tracking-wider text-[#6B7280]"
                >
                  {heading}
                </th>
              ))}
              <th scope="col" className="px-3 py-2.5 text-right text-xs font-semibold uppercase tracking-wider text-[#6B7280]">
                <span className="sr-only">Actions</span>
              </th>
            </tr>
          </thead>

          <tbody>
            {items.map((payment) => {
              const gatewayOrder = gatewayOrderText(payment.razorpayOrderId)

              return (
                <tr
                  key={payment.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-gray-50/60"
                >
                  <td className="px-3 py-3 align-top">
                    <p className="break-words text-[#16181D]">
                      {paymentInvoiceTypeText(payment.invoiceType) ?? (
                        // An unmapped type shows its raw ordinal rather than a guess.
                        // It is rendered as a number, not passed through String(),
                        // so no coercion is involved in producing this label.
                        <span>Unknown type (ordinal {payment.invoiceType})</span>
                      )}
                    </p>
                    <p className="mt-1 break-all font-mono text-[11px] text-[#9CA3AF]">
                      {shortGuid(payment.id)}
                    </p>
                  </td>

                  <td className="px-3 py-3 align-top">
                    <StatusPill
                      label={paymentOrderStatusText(payment.status) ?? 'Unknown'}
                      tone={paymentOrderStatusTone(payment.status)}
                    />
                  </td>

                  <td className="px-3 py-3 align-top">
                    <p className="break-words text-[#16181D]">
                      {presentText(payment.clientCompanyName) ?? MISSING_VALUE}
                    </p>
                  </td>

                  <td className="px-3 py-3 align-top">
                    <LinkedInvoiceCell payment={payment} />
                  </td>

                  <td className="px-3 py-3 align-top">
                    <AmountCell amount={payment.amount} currency={payment.currency} />
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {dateTimeText(payment.createdAt)}
                  </td>

                  <td className="px-3 py-3 align-top">
                    {gatewayOrder ? (
                      <p className="break-all font-mono text-[11px] text-[#6B7280]">
                        {gatewayOrder}
                      </p>
                    ) : (
                      <p className="text-[#9CA3AF]">No gateway order reference</p>
                    )}
                  </td>

                  <td className="px-3 py-3 text-right align-top">
                    <DetailsButton payment={payment} onOpenDetails={onOpenDetails} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((payment) => {
          const gatewayOrder = gatewayOrderText(payment.razorpayOrderId)

          return (
            <li
              key={payment.id}
              className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                  {paymentInvoiceTypeText(payment.invoiceType) ?? 'Payment order'}
                </p>
                <StatusPill
                  label={paymentOrderStatusText(payment.status) ?? 'Unknown'}
                  tone={paymentOrderStatusTone(payment.status)}
                />
              </div>

              <p className="mt-1 break-words text-xs text-[#6B7280]">
                {presentText(payment.clientCompanyName) ?? MISSING_VALUE}
              </p>

              <div className="mt-2">
                <AmountCell amount={payment.amount} currency={payment.currency} />
              </div>

              <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
                <dt className="text-[#6B7280]">Created</dt>
                <dd className="text-[#16181D]">{dateTimeText(payment.createdAt)}</dd>
                <dt className="text-[#6B7280]">Linked invoice</dt>
                <dd>
                  <LinkedInvoiceCell payment={payment} />
                </dd>
              </dl>

              <div className="mt-2.5">
                <p className="text-[11px] text-[#9CA3AF]">Gateway order</p>
                {gatewayOrder ? (
                  <p className="mt-0.5 break-all font-mono text-[11px] text-[#6B7280]">
                    {gatewayOrder}
                  </p>
                ) : (
                  <p className="mt-0.5 text-xs text-[#9CA3AF]">No gateway order reference</p>
                )}
              </div>

              <div className="mt-3 flex justify-end">
                <DetailsButton payment={payment} onOpenDetails={onOpenDetails} />
              </div>
            </li>
          )
        })}
      </ul>

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#9CA3AF]">
        <Wallet size={13} strokeWidth={1.75} className="mt-px shrink-0" aria-hidden="true" />
        <span>
          Invoice type and status arrive as numbers on this DTO, unlike every other
          Admin enum, and are read as numbers here. The linked invoice is shown as an
          id: there is no Admin route that accepts an invoice id, so there is nothing
          to link it to. Amounts are printed with the currency each record actually
          stores and are never added together.
        </span>
      </p>
    </>
  )
}

export default AdminPaymentOrderTable
