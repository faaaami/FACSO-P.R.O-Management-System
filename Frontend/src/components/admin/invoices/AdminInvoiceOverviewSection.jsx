import { FileText } from 'lucide-react'
import StatusPill from '../../client/StatusPill'
import AdminDetailRow from '../clients/AdminDetailRow'
import AdminInvoiceDownloadPdfButton from './AdminInvoiceDownloadPdfButton'
import {
  BILLING_LIFECYCLE_NOTE,
  NO_MONEY_AGGREGATION_NOTE,
  contractReference,
  dateTimeText,
  formatDate,
  moneyParts,
  presentText,
  renewalTaskReference,
  retainerStatusText,
  retainerStatusTone,
  serviceFeeStatusText,
  serviceFeeStatusTone,
} from './billingDisplay'

/**
 * Overview of one invoice, from the Retainer or Service Fee detail DTO.
 *
 * ------------------------------------------------------------------
 * `kind` SWITCHES THE DTO, AND ONLY THE DTO. The two detail responses are NOT the
 * same record and this component never pretends they are:
 * ------------------------------------------------------------------
 *   GetRetainerInvoiceByIdResponseDto
 *       periodStart?, periodEnd?, contractNumber, clientCompanyName, … updatedAt
 *   GetServiceFeeInvoiceByIdResponseDto
 *       description, renewalTaskId, clientCompanyName, … updatedAt
 *
 * The retainer carries a billing PERIOD and a human CONTRACT NUMBER; the Service Fee
 * carries neither. A Service Fee invoice is a single charge, not a period, so
 * showing it a "period" row or a contract number would be reading fields off the
 * wrong DTO. Every block below is therefore rendered conditionally on `kind`, and a
 * field the active DTO does not have is not rendered at all rather than shown as a
 * dash — a dash means "recorded as empty", which is a different claim from "this
 * invoice type has no such field".
 *
 * The shared fields (number, status, client, amount, dates, outcome timestamps,
 * created/updated) really are identical on both, and are rendered once for both.
 *
 * THE AMOUNT IS CURRENCY-AWARE. `formatMoney(amount, currency)` is the canonical
 * formatter, and the raw stored currency code is printed beside it. `currency` is
 * unvalidated free text with no lookup table anywhere in the platform, so AED is
 * never assumed and `clientDisplay.formatCurrency` — which is hardcoded to AED — is
 * not used anywhere in this module. Nothing here is added to anything else.
 */
function AmountRow({ amount, currency }) {
  const money = moneyParts(amount, currency)

  return (
    <div className="grid grid-cols-1 gap-0.5 border-b border-[#E2E4E9] py-2.5 last:border-b-0 sm:grid-cols-[minmax(0,10rem)_minmax(0,1fr)] sm:gap-4">
      <dt className="text-xs font-medium text-[#6B7280]">Amount</dt>
      <dd className="min-w-0 text-sm text-[#16181D]">
        <span className="tabular-nums">{money.formatted}</span>
        <span className="ml-2 text-xs text-[#6B7280]">
          {money.hasCurrency ? `Currency: ${money.rawCurrency}` : 'Currency not recorded'}
        </span>
      </dd>
    </div>
  )
}

function AdminInvoiceOverviewSection({ kind, invoice }) {
  const isRetainer = kind === 'retainer'

  const statusText = isRetainer
    ? retainerStatusText(invoice.status)
    : serviceFeeStatusText(invoice.status)
  const statusTone = isRetainer
    ? retainerStatusTone(invoice.status)
    : serviceFeeStatusTone(invoice.status)

  const contract = isRetainer ? contractReference(invoice) : null
  const task = isRetainer ? null : renewalTaskReference(invoice)

  // The narrative field is read ONLY from the DTO that has it. `notes` exists on
  // GetRetainerInvoiceByIdResponseDto and `description` exists on
  // GetServiceFeeInvoiceByIdResponseDto, and neither is a property of the other, so
  // each is read under its own kind rather than both being read and then discarded.
  const notes = isRetainer ? presentText(invoice.notes) : null
  const description = isRetainer ? null : presentText(invoice.description)
  const voidReason = presentText(invoice.voidReason)

  return (
    <div className="flex flex-col gap-5">
      <section
        aria-labelledby="admin-invoice-overview-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-invoice-overview-heading"
          className="flex items-center gap-1.5 text-sm font-semibold tracking-tight text-[#16181D]"
        >
          <FileText size={14} strokeWidth={1.75} className="text-[#6B7280]" aria-hidden="true" />
          {isRetainer ? 'Retainer invoice' : 'Service Fee invoice'}
        </h3>

        <div className="mt-3 flex flex-wrap items-center gap-2">
          <StatusPill label={statusText ?? 'Unknown'} tone={statusTone} />
          <span className="rounded-[6px] border border-[#E2E4E9] bg-[#F7F8FA] px-2 py-0.5 text-xs font-medium text-[#6B7280]">
            {isRetainer ? 'Retainer' : 'Service Fee'}
          </span>
        </div>

        <dl className="mt-3.5 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
          <AdminDetailRow label="Invoice number" value={invoice.invoiceNumber} />
          <AdminDetailRow label="Client company" value={invoice.clientCompanyName} />
          <AdminDetailRow label="Invoice ID" value={invoice.id} mono />

          {/* Retainer only: a contract number exists nowhere on the Service Fee DTO. */}
          {isRetainer ? (
            <>
              <AdminDetailRow label="Contract number" value={contract?.number} />
              <AdminDetailRow label="Service contract" value={contract?.label} mono />
            </>
          ) : (
            /* Service Fee only: a renewal task, id-only on both list and detail. */
            <AdminDetailRow label="Renewal task" value={task?.label} mono />
          )}

          <AmountRow amount={invoice.amount} currency={invoice.currency} />

          <AdminDetailRow label="Invoice date" value={formatDate(invoice.invoiceDate)} />

          {isRetainer && (
            <>
              {/* A retainer invoice covers a period; a Service Fee charge does not,
                  so these two rows exist only for the retainer. */}
              <AdminDetailRow label="Period start" value={formatDate(invoice.periodStart)} />
              <AdminDetailRow label="Period end" value={formatDate(invoice.periodEnd)} />
            </>
          )}

          <AdminDetailRow label="Due date" value={formatDate(invoice.dueDate)} />
        </dl>
      </section>

      <section
        aria-labelledby="admin-invoice-outcome-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-invoice-outcome-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Outcome
        </h3>

        <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
          <AdminDetailRow label="Paid at" value={dateTimeText(invoice.paidAt)} />
          <AdminDetailRow label="Voided at" value={dateTimeText(invoice.voidedAt)} />
          {voidReason && <AdminDetailRow label="Void reason" value={voidReason} />}
        </dl>

        {!invoice.paidAt && !invoice.voidedAt ? (
          <p className="mt-2.5 text-xs text-[#9CA3AF]">
            Neither a paid nor a void timestamp is recorded, so this invoice has
            reached neither terminal state.
          </p>
        ) : null}
      </section>

      {(notes || description) && (
        <section
          aria-labelledby="admin-invoice-notes-heading"
          className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
        >
          <h3
            id="admin-invoice-notes-heading"
            className="text-sm font-semibold tracking-tight text-[#16181D]"
          >
            {isRetainer ? 'Notes' : 'Description'}
          </h3>

          <p className="mt-2.5 whitespace-pre-wrap break-words text-sm leading-relaxed text-[#16181D]">
            {isRetainer ? notes : description}
          </p>
        </section>
      )}

      <section
        aria-labelledby="admin-invoice-record-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-white p-4 shadow-[0_1px_3px_rgba(28,31,38,0.06)]"
      >
        <h3
          id="admin-invoice-record-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Record
        </h3>

        <dl className="mt-3 grid grid-cols-1 gap-x-6 sm:grid-cols-2">
          <AdminDetailRow label="Created" value={dateTimeText(invoice.createdAt)} />
          <AdminDetailRow label="Last updated" value={dateTimeText(invoice.updatedAt)} />
        </dl>

        <div className="mt-3.5 border-t border-[#E2E4E9] pt-3.5">
          <p className="text-xs font-semibold text-[#6B7280]">Invoice PDF</p>
          <div className="mt-2">
            <AdminInvoiceDownloadPdfButton
              kind={isRetainer ? 'retainer' : 'service-fee'}
              id={invoice.id}
              invoiceNumber={invoice.invoiceNumber}
            />
          </div>
          <p className="mt-2 text-[11px] leading-relaxed text-[#9CA3AF]">
            Fetched on demand from the invoice&rsquo;s own PDF endpoint and opened in
            a new tab. The response is the file itself rather than a JSON envelope,
            so a failure here reports the server&rsquo;s own message instead of a
            generic download error.
          </p>
        </div>
      </section>

      {/*
        This section used to be headed "Read-only record" and carried READ_ONLY_NOTE.
        Both are gone because the claim is now false: the record CAN be changed, and
        the two controls that change it sit in the drawer's action area below. The
        replacement copy states the lifecycle and the irreversibility, so the reader
        learns that here rather than being surprised by an irreversible button.

        The money-aggregation note stays: it is still true, and it explains the absent
        totals rather than leaving their absence to be guessed at.
      */}
      <section
        aria-labelledby="admin-invoice-lifecycle-heading"
        className="rounded-[12px] border border-[#E2E4E9] bg-[#F7F8FA] p-4"
      >
        <h3
          id="admin-invoice-lifecycle-heading"
          className="text-sm font-semibold tracking-tight text-[#16181D]"
        >
          Record lifecycle
        </h3>

        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          {BILLING_LIFECYCLE_NOTE}
        </p>
        <p className="mt-2 text-xs leading-relaxed text-[#6B7280]">
          {NO_MONEY_AGGREGATION_NOTE}
        </p>
      </section>
    </div>
  )
}

export default AdminInvoiceOverviewSection
