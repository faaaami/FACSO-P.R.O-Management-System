import { useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Info, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../client/settings/FormField'
import { useFocusTrap } from '../../hooks/useFocusTrap'
import { apiFieldErrors } from '../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../utils/apiError'

/**
 * Invites a client contact (Admin, or an Agent for an assigned client).
 *
 * THERE IS NO PASSWORD FIELD. The server emails the contact a single-use link to
 * set their own password, so nobody else ever knows it. The contact still needs
 * Admin approval before they can sign in, and the note says so up front.
 *
 * `createContact` is a react-query mutation whose variables are
 * { clientId, fullName, email, phone? } and whose result is the created contact,
 * including `invitationSent`.
 *
 * EMAIL UNIQUENESS IS GLOBAL: the address is checked against every non-deleted
 * user (staff and other companies' contacts included), so a 409 is reported on
 * the email field with that scope in the message.
 */
const schema = z.object({
  fullName: z.string().trim().min(1, 'Full name is required.').max(200, 'Full name must be 200 characters or fewer.'),
  email: z
    .string()
    .trim()
    .min(1, 'Email is required.')
    .max(255, 'Email must be 255 characters or fewer.')
    .refine((value) => z.string().email().safeParse(value).success, {
      message: 'Enter a valid email address.',
    }),
  phone: z.string().max(30, 'Phone must be 30 characters or fewer.'),
})

function ClientContactInviteDialog({ clientId, onClose, onCreated, createContact }) {
  const titleId = useId()
  const noteId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', email: '', phone: '' },
  })

  function onSubmit(values) {
    setErrorMessage(null)

    createContact.mutate(
      {
        clientId,
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        // Omitted rather than sent as "": "" is a real value to the handler.
        phone: values.phone.trim() || undefined,
      },
      {
        onSuccess: (created) => {
          if (created?.invitationSent === false) {
            toast.error(
              `${created.fullName} was added, but the invitation email could not be sent. Use "Resend invitation" to try again.`,
            )
          } else {
            toast.success(`Invitation sent to ${created?.email ?? values.email.trim()}.`)
          }
          onCreated?.(created)
        },
        onError: (error) => handleSubmitError(error, setError, setErrorMessage),
      },
    )
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div className="fixed inset-0 bg-slate-900/40" onClick={onClose} aria-hidden="true" />

      <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <h2 id={titleId} className="text-base font-semibold tracking-tight text-[#16181D]">
            Invite client contact
          </h2>

          <p className="mt-1 text-xs text-[#6B7280]">
            This creates a sign-in account for this company&apos;s client portal.
          </p>

          <div
            id={noteId}
            role="note"
            className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/70 px-3.5 py-3"
          >
            <Info size={15} strokeWidth={2} className="mt-0.5 shrink-0 text-[#6B7280]" aria-hidden="true" />
            <div className="min-w-0 text-xs text-[#6B7280]">
              <p>
                The contact receives an email with a link to set their own password. The link
                works once and expires after 72 hours; it can be resent.
              </p>
              <p className="mt-1">
                The account must also be approved by an Admin before the contact can sign in.
              </p>
            </div>
          </div>

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <AlertCircle size={14} strokeWidth={2} className="shrink-0 text-[#DC2626]" aria-hidden="true" />
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <form
            onSubmit={handleSubmit(onSubmit)}
            noValidate
            aria-describedby={noteId}
            className="mt-5 flex flex-col gap-4"
          >
            <FormField label="Full name" htmlFor="inviteContactFullName" error={errors.fullName?.message}>
              <input
                id="inviteContactFullName"
                type="text"
                autoComplete="name"
                disabled={createContact.isPending}
                className={errors.fullName ? inputErrorClass : inputClass}
                {...register('fullName')}
              />
            </FormField>

            <FormField label="Email" htmlFor="inviteContactEmail" error={errors.email?.message}>
              <input
                id="inviteContactEmail"
                type="email"
                autoComplete="off"
                disabled={createContact.isPending}
                className={errors.email ? inputErrorClass : inputClass}
                {...register('email')}
              />
            </FormField>

            <FormField label="Phone" htmlFor="inviteContactPhone" error={errors.phone?.message}>
              <input
                id="inviteContactPhone"
                type="tel"
                autoComplete="tel"
                disabled={createContact.isPending}
                className={errors.phone ? inputErrorClass : inputClass}
                {...register('phone')}
              />
            </FormField>

            <p className="text-xs text-[#6B7280]">
              The email must be unique across every user account in the system, not just this
              company&apos;s contacts. Name, email and phone cannot be changed after this.
            </p>

            <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={createContact.isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                // A double submit would create two accounts.
                disabled={createContact.isPending}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {createContact.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Sending invitation…</span>
                  </>
                ) : (
                  <span>Send invitation</span>
                )}
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  )
}

/**
 * A 409 is always the email (already used by another account); a 404 means the
 * company is gone or outside the caller's scope.
 */
function handleSubmitError(error, setError, setErrorMessage) {
  if (error?.response?.status === 409) {
    setError('email', {
      type: 'server',
      message: extractApiErrorMessage(error, 'That email address already belongs to another account.'),
    })
    return
  }

  if (error?.response?.status === 404) {
    const message = 'This company is no longer available, so no contact can be added to it.'
    setErrorMessage(message)
    toast.error(message)
    return
  }

  const failedFields = Object.entries(apiFieldErrors(error))
  if (failedFields.length > 0) {
    failedFields.forEach(([field, message]) => setError(field, { type: 'server', message }))
    return
  }

  const message = extractApiErrorMessage(error, 'Unable to invite this contact. Please try again.')
  setErrorMessage(message)
  toast.error(message)
}

export default ClientContactInviteDialog
