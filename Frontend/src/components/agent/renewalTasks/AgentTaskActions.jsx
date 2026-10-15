import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Ban, Loader2, Save } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { RENEWAL_TASK_STATUS } from '../../client/enumLabels'
import { useAgentTaskActions } from '../../../hooks/agent/useAgentTaskActions'
import { extractApiErrorMessage } from '../../../utils/apiError'

const COMPLETING_STATUS = 'Updated'

/**
 * The server requires a strictly future instant, and a date-only value is read
 * as that day's midnight — so today is already in the past by the time the
 * request is processed. Tomorrow is therefore the first selectable day.
 */
const earliestSelectableExpiry = () =>
  new Date(Date.now() + 86400000).toISOString().slice(0, 10)

const statusSchema = z
  .object({
    status: z.string().min(1, 'Status is required.'),
    note: z.string().max(1000, 'Note must be at most 1000 characters.').optional(),
    renewedExpiryDate: z.string().optional(),
  })
  .superRefine((values, ctx) => {
    if (values.status !== COMPLETING_STATUS) return

    if (!values.renewedExpiryDate) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['renewedExpiryDate'],
        message: 'Enter the new expiry date printed on the renewed document.',
      })
      return
    }

    const chosen = new Date(`${values.renewedExpiryDate}T00:00:00Z`)

    if (Number.isNaN(chosen.getTime()) || chosen.getTime() <= Date.now()) {
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['renewedExpiryDate'],
        message: 'The renewed expiry date must be in the future.',
      })
    }
  })

const blockSchema = z.object({
  blockedReason: z.string().trim().min(1, 'A blocked reason is required.'),
})

function ErrorBanner({ message }) {
  if (!message) return null
  return (
    <div className="flex items-start gap-2 rounded-[10px] border border-red-200 bg-red-50/60 px-3.5 py-2.5">
      <AlertCircle size={14} strokeWidth={2} className="mt-0.5 shrink-0 text-[#DC2626]" aria-hidden="true" />
      <p className="text-xs font-medium text-[#DC2626]">{message}</p>
    </div>
  )
}

function AgentTaskActions({ taskId, documentId, currentStatus }) {
  const { updateStatus, block } = useAgentTaskActions()
  const [statusError, setStatusError] = useState(null)
  const [blockError, setBlockError] = useState(null)

  const statusForm = useForm({
    resolver: zodResolver(statusSchema),
    defaultValues: { status: currentStatus ?? '', note: '', renewedExpiryDate: '' },
  })

  const blockForm = useForm({
    resolver: zodResolver(blockSchema),
    defaultValues: { blockedReason: '' },
  })

  const selectedStatus = statusForm.watch('status')
  const isCompleting = selectedStatus === COMPLETING_STATUS

  function submitStatus(values) {
    setStatusError(null)
    const note = values.note?.trim() ? values.note.trim() : undefined
    // Only a completion carries the date, so the field is withheld on every
    // other transition rather than sent as a stale leftover.
    const renewedExpiryDate = isCompleting ? values.renewedExpiryDate : undefined

    updateStatus.mutate(
      { taskId, documentId, status: values.status, note, renewedExpiryDate },
      {
        onSuccess: () => {
          statusForm.reset({
            status: values.status,
            note: '',
            renewedExpiryDate: '',
          })
          toast.success(
            isCompleting
              ? 'Task completed and the document expiry was updated.'
              : 'Task status updated.',
          )
        },
        onError: (error) => {
          const message = extractApiErrorMessage(error, 'Could not update the task status.')
          setStatusError(message)
          toast.error(message)
        },
      }
    )
  }

  function submitBlock(values) {
    setBlockError(null)

    block.mutate(
      { taskId, blockedReason: values.blockedReason.trim() },
      {
        onSuccess: () => {
          blockForm.reset({ blockedReason: '' })
          toast.success('Task blocked.')
        },
        onError: (error) => {
          const message = extractApiErrorMessage(error, 'Could not block the task.')
          setBlockError(message)
          toast.error(message)
        },
      }
    )
  }

  const isBusy = updateStatus.isPending || block.isPending

  return (
    <div className="rounded-[8px] border border-[#E2E4E9] bg-white p-3">
      <span className="block text-[11px] font-medium text-[#6B7280] uppercase tracking-wider">
        Actions
      </span>

      <form onSubmit={statusForm.handleSubmit(submitStatus)} noValidate className="mt-3 space-y-3">
        <FormField label="Status" htmlFor="agentTaskStatus" error={statusForm.formState.errors.status?.message}>
          <select
            id="agentTaskStatus"
            disabled={isBusy}
            className={statusForm.formState.errors.status ? inputErrorClass : inputClass}
            {...statusForm.register('status')}
          >
            <option value="">Select a status</option>
            {Object.keys(RENEWAL_TASK_STATUS).map((key) => (
              <option key={key} value={key}>
                {RENEWAL_TASK_STATUS[key]}
              </option>
            ))}
          </select>
        </FormField>

        <FormField label="Note (optional)" htmlFor="agentTaskStatusNote" error={statusForm.formState.errors.note?.message}>
          <textarea
            id="agentTaskStatusNote"
            rows={2}
            disabled={isBusy}
            placeholder="Optional context for this status change"
            className={`${statusForm.formState.errors.note ? inputErrorClass : inputClass} resize-y`}
            {...statusForm.register('note')}
          />
        </FormField>

        {isCompleting && (
          <FormField
            label="Renewed expiry date"
            htmlFor="agentTaskRenewedExpiry"
            error={statusForm.formState.errors.renewedExpiryDate?.message}
          >
            <input
              id="agentTaskRenewedExpiry"
              type="date"
              min={earliestSelectableExpiry()}
              disabled={isBusy}
              className={
                statusForm.formState.errors.renewedExpiryDate
                  ? inputErrorClass
                  : inputClass
              }
              {...statusForm.register('renewedExpiryDate')}
            />
          </FormField>
        )}

        {isCompleting && (
          <p className="text-xs text-[#6B7280]">
            Saving this will replace the expiry date on the linked document, so it
            stops appearing as due for renewal.
          </p>
        )}

        <ErrorBanner message={statusError} />

        <button
          type="submit"
          disabled={updateStatus.isPending || block.isPending}
          className="inline-flex items-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
        >
          {updateStatus.isPending ? (
            <Loader2 size={16} strokeWidth={2} className="animate-spin" aria-hidden="true" />
          ) : (
            <Save size={16} strokeWidth={1.75} aria-hidden="true" />
          )}
          <span>{updateStatus.isPending ? 'Updating...' : 'Update status'}</span>
        </button>
      </form>

      <div className="my-4 border-t border-[#E2E4E9]" />

      <form onSubmit={blockForm.handleSubmit(submitBlock)} noValidate className="space-y-3">
        <FormField
          label="Blocked reason"
          htmlFor="agentTaskBlockedReason"
          error={blockForm.formState.errors.blockedReason?.message}
        >
          <textarea
            id="agentTaskBlockedReason"
            rows={2}
            disabled={isBusy}
            placeholder="Why is this task blocked?"
            className={`${blockForm.formState.errors.blockedReason ? inputErrorClass : inputClass} resize-y`}
            {...blockForm.register('blockedReason')}
          />
        </FormField>

        <ErrorBanner message={blockError} />

        <button
          type="submit"
          disabled={block.isPending || updateStatus.isPending}
          className="inline-flex items-center gap-2 rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:opacity-50 disabled:cursor-not-allowed transition duration-150 cursor-pointer"
        >
          {block.isPending ? (
            <Loader2 size={16} strokeWidth={2} className="animate-spin" aria-hidden="true" />
          ) : (
            <Ban size={16} strokeWidth={1.75} aria-hidden="true" />
          )}
          <span>{block.isPending ? 'Blocking...' : 'Block task'}</span>
        </button>
      </form>
    </div>
  )
}

export default AgentTaskActions
