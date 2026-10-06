import { Eye, Landmark } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import {
  MISSING_VALUE,
  dateText,
  govFeeStatusText,
  govFeeStatusTone,
  govFeeTaskReference,
  moneyParts,
  presentText,
  shortGuid,
} from './billingDisplay'

/**
 * Admin government-fee disbursement list. From GovFeeDisbursementDto.
 *
 * ------------------------------------------------------------------
 * THIS IS NOT AN INVOICE, AND THE TABLE DOES NOT CALL IT ONE.
 * ------------------------------------------------------------------
 * A disbursement is the firm having PAID a government fee on a client's behalf and
 * waiting to be reimbursed. It has no invoice number, no due date, no Paid/Void
 * lifecycle and no void reason, and calling it an invoice here would imply a
 * document and a lifecycle that do not exist. The columns are therefore
 * Paid-by-firm, Invoiced-to-client and Reimbursed — the three points on its own
 * timeline — and the empty state and copy say "disbursement" throughout.
 *
 * `InvoicedToClient` is a TIMESTAMP on the record, not an invoice number: the firm
 * has billed the client, and the column shows when.
 *
 * THE COMPANY NAME IS ALREADY ON THE DTO. `GovFeeDisbursementDto` carries
 * ClientCompanyName, joined server-side, so this table performs NO client lookup at
 * all — unlike the two invoice tables, which must resolve a bare id. The difference
 * is a property of the DTOs, not an inconsistency in this module.
 *
 * THE RENEWING TASK IS OPTIONAL AND ID-ONLY. `renewalTaskId` is a nullable Guid:
 * a government fee is not always tied to a renewal task. It is shown as a short
 * reference where present and reported as absent where not, and never as a name —
 * and note that the list endpoint binds NO task filter, so it cannot be narrowed by
 * one even though the column exists.
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

function DescriptionCell({ disbursement }) {
  const task = govFeeTaskReference(disbursement)
  const description = presentText(disbursement.feeDescription)

  return (
    <div className="min-w-0">
      <p className="break-words text-[#16181D]">{description ?? MISSING_VALUE}</p>
      {task.present ? (
        <p
          className="mt-0.5 break-all font-mono text-[11px] text-[#9CA3AF]"
          title={String(task.id)}
        >
          Renewal task {task.label}
        </p>
      ) : (
        <p className="mt-0.5 text-[11px] text-[#9CA3AF]">Not tied to a renewal task</p>
      )}
    </div>
  )
}

function DetailsButton({ disbursement, onOpenDetails }) {
  return (
    <button
      type="button"
      onClick={() => onOpenDetails(disbursement.id)}
      aria-label={`View details for the government fee ${presentText(disbursement.feeDescription) ?? shortGuid(disbursement.id)}`}
      className="inline-flex cursor-pointer items-center gap-1.5 rounded-[8px] border border-[#E2E4E9] bg-white px-2.5 py-1 text-xs font-semibold text-[#16181D] transition duration-150 hover:bg-[#F7F8FA] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
    >
      <Eye size={13} strokeWidth={1.75} aria-hidden="true" />
      Details
    </button>
  )
}

function AdminGovFeeDisbursementTable({ items, onOpenDetails }) {
  return (
    <>
      <div className="hidden overflow-x-auto md:block">
        <table className="w-full min-w-[980px] border-collapse text-left">
          <caption className="sr-only">
            Government fee disbursements, newest first. Each row shows the fee
            description, status, client company, government reference, amount with
            its stored currency, and the dates the firm paid, invoiced the client
            and was reimbursed, with a Details action that opens the full
            disbursement.
          </caption>

          <thead>
            <tr className="border-b border-[#E2E4E9]">
              {[
                'Fee description',
                'Status',
                'Client company',
                'Government reference',
                'Amount',
                'Paid by firm',
                'Reimbursed',
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
            {items.map((disbursement) => {
              const reference = presentText(disbursement.governmentReference)

              return (
                <tr
                  key={disbursement.id}
                  className="border-b border-[#E2E4E9] transition-colors last:border-b-0 hover:bg-gray-50/60"
                >
                  <td className="px-3 py-3 align-top">
                    <DescriptionCell disbursement={disbursement} />
                  </td>

                  <td className="px-3 py-3 align-top">
                    <StatusPill
                      label={govFeeStatusText(disbursement.status) ?? 'Unknown'}
                      tone={govFeeStatusTone(disbursement.status)}
                    />
                  </td>

                  <td className="px-3 py-3 align-top">
                    <p className="break-words text-[#16181D]">
                      {presentText(disbursement.clientCompanyName) ?? MISSING_VALUE}
                    </p>
                    <p className="mt-1 break-all font-mono text-[11px] text-[#9CA3AF]">
                      {shortGuid(disbursement.id)}
                    </p>
                  </td>

                  <td className="px-3 py-3 align-top">
                    {reference ? (
                      <p className="break-words text-[#16181D]">{reference}</p>
                    ) : (
                      <p className="text-[#9CA3AF]">No reference recorded</p>
                    )}
                  </td>

                  <td className="px-3 py-3 align-top">
                    <AmountCell
                      amount={disbursement.amount}
                      currency={disbursement.currency}
                    />
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {dateText(disbursement.paidByFirmAt)}
                  </td>

                  <td className="px-3 py-3 align-top text-[#16181D]">
                    {disbursement.reimbursedAt ? (
                      dateText(disbursement.reimbursedAt)
                    ) : (
                      <span className="text-[#9CA3AF]">Not reimbursed</span>
                    )}
                  </td>

                  <td className="px-3 py-3 text-right align-top">
                    <DetailsButton
                      disbursement={disbursement}
                      onOpenDetails={onOpenDetails}
                    />
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>

      <ul className="flex flex-col gap-2.5 md:hidden">
        {items.map((disbursement) => (
          <li
            key={disbursement.id}
            className="rounded-[12px] border border-[#E2E4E9] bg-white p-3.5 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
          >
            <div className="flex items-start justify-between gap-2">
              <p className="min-w-0 break-words text-sm font-semibold text-[#16181D]">
                {presentText(disbursement.feeDescription) ?? MISSING_VALUE}
              </p>
              <StatusPill
                label={govFeeStatusText(disbursement.status) ?? 'Unknown'}
                tone={govFeeStatusTone(disbursement.status)}
              />
            </div>

            <p className="mt-1 break-words text-xs text-[#6B7280]">
              {presentText(disbursement.clientCompanyName) ?? MISSING_VALUE}
            </p>

            <div className="mt-2">
              <AmountCell
                amount={disbursement.amount}
                currency={disbursement.currency}
              />
            </div>

            <dl className="mt-2 grid grid-cols-2 gap-x-3 gap-y-1 text-xs">
              <dt className="text-[#6B7280]">Paid by firm</dt>
              <dd className="text-[#16181D]">{dateText(disbursement.paidByFirmAt)}</dd>
              <dt className="text-[#6B7280]">Invoiced to client</dt>
              <dd className="text-[#16181D]">
                {disbursement.invoicedAt ? dateText(disbursement.invoicedAt) : 'Not yet'}
              </dd>
              <dt className="text-[#6B7280]">Reimbursed</dt>
              <dd className="text-[#16181D]">
                {disbursement.reimbursedAt
                  ? dateText(disbursement.reimbursedAt)
                  : 'Not reimbursed'}
              </dd>
              <dt className="text-[#6B7280]">Reference</dt>
              <dd className="break-words text-[#16181D]">
                {presentText(disbursement.governmentReference) ?? 'None recorded'}
              </dd>
            </dl>

            <div className="mt-2.5">
              <DescriptionCell disbursement={disbursement} />
            </div>

            <div className="mt-3 flex justify-end">
              <DetailsButton
                disbursement={disbursement}
                onOpenDetails={onOpenDetails}
              />
            </div>
          </li>
        ))}
      </ul>

      <p className="mt-3 flex items-start gap-1.5 text-xs text-[#9CA3AF]">
        <Landmark size={13} strokeWidth={1.75} className="mt-px shrink-0" aria-hidden="true" />
        <span>
          A government fee is a disbursement, not an invoice: the firm has paid a
          government fee and is waiting to be reimbursed, so there is no invoice
          number, no due date and no void state on this record. Amounts are printed
          with the currency each record actually stores and are never added together.
        </span>
      </p>
    </>
  )
}

export default AdminGovFeeDisbursementTable
