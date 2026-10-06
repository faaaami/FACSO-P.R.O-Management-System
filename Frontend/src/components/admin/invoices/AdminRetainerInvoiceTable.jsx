import { Eye, FileText } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  MISSING_VALUE,
  contractReference,
  dateText,
  moneyParts,
  presentText,
  retainerStatusText,
  retainerStatusTone,
  shortGuid,
} from './billingDisplay'

/**
 * Admin retainer-invoice list. From GetRetainerInvoiceListItemDto.
 *
 * RESPONSIVE: a real <table> from `md` up inside an overflow container, and a <ul>
 * card list below it. The table needs a min-width because eight columns of wrapped
 * text cannot compress further, so below `md` it is replaced rather than squeezed —
 * a horizontally scrolling table is unusable on a phone. Both renderings iterate the
 * same `items`, so they can never be out of step, and each is hidden from assistive
 * tech as well as from view at the other breakpoint.
 *
 * THE COMPANY NAME IS RESOLVED, THE CONTRACT IS NOT. The list DTO carries
 * `clientCompanyId` with no name, and the shared entity map resolves it. It also
 * carries `serviceContractId`, and NOTHING resolves that: the Admin API has no
 * global contracts list and no /renewal-tasks page that accepts a task id, so there
 * is no destination to link to. It is shown as a short reference, with the full
 * value in `title`, and never as a contract number — `contractNumber` does not
 * exist on this DTO and belongs only to the drawer, where the detail endpoint
 * supplies it.
 *
 * THE AMOUNT IS CURRENCY-AWARE AND NEVER AGGREGATED. The formatter is
 * `formatMoney(amount, currency)`, the same canonical helper the client billing
 * module uses, and the raw stored currency code is printed next to it. The column
 * is deliberately NOT assumed to be AED: `currency` is an unvalidated free-text
 * column, so `clientDisplay.formatCurrency` would be wrong here. No amount on this
 * table is ever added to another.
 *
 * THE OUTCOME COLUMN IS A COLLAPSE, NOT A SUMMARY. Paid and Void are mutually
 * exclusive terminal states, so one cell renders whichever terminal timestamp the
 * DTO carries, plus the void reason when there is one. A Pending invoice carries
 * neither, and says so.
 */
function AmountCell({ amount, currency }) {
  const money = moneyParts(amount, currency)

  return (
    <div className="min-w-0">
      <p className="tabular-nums text-[#16181D]">{money.formatted}</p>
      {/*
        The raw code stays visible even when formatting succeeded, because the
        column is unvalidated free text and the stored value is what an auditor needs
        to see. When it is absent the row says so rather than letting the
        formatter's AED default speak for it.
      */}
      <p className="mt-0.5 text-[11px] text-[#9CA3AF]">
        {money.hasCurrency ? `Currency: ${money.rawCurrency}` : 'Currency not recorded'}
      </p>
    </div>
  )
}

function OutcomeCell({ invoice }) {
  if (invoice.paidAt) {
    return (
      <div className="min-w-0">
        <p className="text-[#16181D]">Paid {dateText(invoice.paidAt)}</p>
      </div>
    )
  }

  if (invoice.voidedAt) {
    return (
      <div className="min-w-0">
        <p className="text-[#16181D]">Voided {dateText(invoice.voidedAt)}</p>
        {presentText(invoice.voidReason) ? (
          <p className="mt-0.5 break-words text-xs text-[#6B7280]">{invoice.voidReason}</p>
        ) : (
          <p className="mt-0.5 text-xs text-[#9CA3AF]">No reason recorded</p>
        )}
      </div>
    )
  }

  return <span className="text-[#9CA3AF]">Not paid or voided</span>
}

function DetailsButton({ invoice, onOpenDetails }) {
  return (
    <button
      type="button"
      onClick={() => onOpenDetails(invoice.id)}
      aria-label={`View details for retainer invoice ${invoice.invoiceNumber ?? shortGuid(invoice.id)}`}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Eye size={13} strokeWidth={1.75} aria-hidden="true" />
      Details
    </button>
  )
}

function AdminRetainerInvoiceTable({ items, resolveCompany, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[1080px] border-collapse text-left">
          <caption className="sr-only">
            Retainer invoices, newest first. Each row shows the invoice number,
            status, client company, linked service contract, amount with its stored
            currency, invoice and due dates and the payment outcome, with a Details
            action that opens the full invoice.
          </caption>

          <thead>
            <tr className="border-b border-[#E2E4E9]">
              {[
                'Invoice number',
                'Status',
                'Client company',
                'Contract',
                'Amount',
                'Invoice date',
                'Due date',
                'Outcome',
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
            {items.map((invoice) => {
              const company = resolveCompany(invoice.clientCompanyId)
              const contract = contractReference(invoice)

              return (
                <tr
                  key={invoice.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-gray-50/60"
                >
                  <td className="px-3 py-3 align-top">
                    <p className="break-words text-[#16181D]">
                      {invoice.invoiceNumber ?? MISSING_VALUE}
                    </p>
                    <p className="mt-1 break-all font-mono text-[11px] text-[#9CA3AF]">
                      {shortGuid(invoice.id)}
                    </p>
                  </td>

                  <td className="px-3 py-3 align-top">
                    <StatusPill
                      label={retainerStatusText(invoice.status) ?? 'Unknown'}
                      tone={retainerStatusTone(invoice.status)}
                    />
                  </td>

                  <td className="px-3 py-3 align-top">
                    {company?.resolved ? (
                      <p className="break-words text-[#16181D]">{company.name}</p>
                    ) : (
                      <p className="break-words text-[#9CA3AF]">
                        Unresolved company &middot; {shortGuid(invoice.clientCompanyId)}
                      </p>
                    )}
                  </td>

                  <td className="px-3 py-3 align-top">
                    {contract.present ? (
                      <p
                        className="break-all font-mono text-[11px] text-[#6B7280]"
                        title={String(contract.id)}
                      >
                        {contract.label}
                      </p>
                    ) : (
                      <p className="text-[#9CA3AF]">{MISSING_VALUE}</p>
                    )}
                  </td>

                  <td className="px-3 py-3 align-top">
                    <AmountCell amount={invoice.amount} currency={invoice.currency} />
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {dateText(invoice.invoiceDate)}
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {dateText(invoice.dueDate)}
                  </td>

                  <td className="px-3 py-3 align-top">
                    <OutcomeCell invoice={invoice} />
                  </td>

                  <td className="px-3 py-3 text-right align-top">
                    <DetailsButton invoice={invoice} onOpenDetails={onOpenDetails} />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((invoice) => {
          const company = resolveCompany(invoice.clientCompanyId)
          const contract = contractReference(invoice)

          return (
            <li
              key={invoice.id}
              className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
            >
              <div className="flex items-start justify-between gap-2">
                <p className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                  {invoice.invoiceNumber ?? MISSING_VALUE}
                </p>
                <StatusPill
                  label={retainerStatusText(invoice.status) ?? 'Unknown'}
                  tone={retainerStatusTone(invoice.status)}
                />
              </div>

              <p className="mt-1 break-words text-xs text-[#6B7280]">
                {company?.resolved ? company.name : `Unresolved company · ${shortGuid(invoice.clientCompanyId)}`}
              </p>

              <div className="mt-2">
                <AmountCell amount={invoice.amount} currency={invoice.currency} />
              </div>

              <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
                <dt className="text-[#6B7280]">Invoiced</dt>
                <dd className="text-[#16181D]">{dateText(invoice.invoiceDate)}</dd>
                <dt className="text-[#6B7280]">Due</dt>
                <dd className="text-[#16181D]">{dateText(invoice.dueDate)}</dd>
                <dt className="text-[#6B7280]">Contract</dt>
                <dd className="break-all font-mono text-[11px] text-[#6B7280]">
                  {contract.present ? contract.label : MISSING_VALUE}
                </dd>
              </dl>

              <div className="mt-2.5">
                <OutcomeCell invoice={invoice} />
              </div>

              <div className="mt-3 flex justify-end">
                <DetailsButton invoice={invoice} onOpenDetails={onOpenDetails} />
              </div>
            </li>
          )
        })}
      </ul>

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#9CA3AF]">
        <FileText size={13} strokeWidth={1.75} className="mt-px shrink-0" aria-hidden="true" />
        <span>
          Company names are resolved from the Admin client lookup; the service
          contract is shown as an id because the Admin API has no global contract
          list and no page that accepts a contract id, so there is nothing to link
          it to. Amounts are printed with the currency each record actually stores
          and are never added together.
        </span>
      </p>
    </>
  )
}

export default AdminRetainerInvoiceTable
