import { useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, Eye, EyeOff, Info, Loader2 } from 'lucide-react'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import { useCreateAdminClientContact } from '../../../hooks/admin/useAdminClientMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { toast } from 'sonner'

/**
 * Create dialog for a client contact — the one onboarding step that produces a
 * sign-in account.
 *
 * THE ADMIN CHOOSES THE PASSWORD. It is a required field, sent once in this
 * request, and the server returns no credential at all. The Admin already knows
 * the value, so showing it back would be pure disclosure, and the password is
 * never echoed into a toast, an error message or a cache — the only place it
 * exists is this form and the HTTPS request that carries it.
 *
 * WHY THERE IS A CONFIRM FIELD. There is no Admin route to set or reset a
 * contact's password — `PATCH /contacts/{contactId}` writes only `IsActive`. So
 * a mistyped password cannot be corrected; the contact has to be deleted and
 * recreated. That makes a typo a destructive outcome rather than an annoying
 * one, which is what the confirm field exists to prevent.
 *
 * THE PASSWORD IS SHARED MANUALLY. It is never sent by email and never returned
 * by the server, because the Admin is the one who knows which channel the client
 * trusts. The invitation email still goes out, but it carries no credential.
 *
 * WHERE THE PASSWORD DOES NOT GO. This component never stores it beyond the
 * form state, never toasts it, never passes it through an error message and
 * never logs it. `onCreated` hands the response to the drawer, and that response
 * contains no password.
 *
 * NO ROLE CONTROL. The handler hard-codes Role = Client and the DTO has no role
 * field at all. A selector would be sending a field the endpoint does not have.
 * The generated role is stated in the note below rather than chosen.
 *
 * EMAIL UNIQUENESS IS GLOBAL, so a 409 is expected and normal here. The handler
 * checks the address against every non-deleted user, not just this company's
 * contacts, so an address that already belongs to an agent, a staff account or
 * another company's contact is rejected however unrelated. That is a real
 * constraint on onboarding — a client contact cannot reuse the address of the
 * agent who handles them — and the message says so rather than saying "duplicate
 * contact", which would send the Admin looking in the wrong list.
 *
 * THE CONTACT IS NOT USABLE IMMEDIATELY, and the form says so before the Admin
 * submits rather than after. Creation sets IsApproved = false, so the account
 * exists but cannot sign in until the approval action is taken. The email address
 * itself is already verified — there is no verification step and no route for one
 * — so this is an approval gate, not a mailbox confirmation.
 *
 * NO COMPLEXITY RULE IS INVENTED. The server requires 8 to 100 characters and
 * nothing else, so the form says exactly that. Inventing a complexity rule here
 * would let an Admin pick a password the server accepts and a later change to
 * the policy would silently disagree with this form.
 */
const schema = z
  .object({
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
    password: z
      .string()
      .min(1, 'Password is required.')
      .min(8, 'Password must be at least 8 characters.')
      .max(100, 'Password must be 100 characters or fewer.'),
    confirmPassword: z.string().min(1, 'Please confirm the password.'),
  })
  .refine((values) => values.confirmPassword === values.password, {
    message: 'Passwords do not match.',
    path: ['confirmPassword'],
  })

function AdminClientContactFormDialog({ clientId, onClose, onCreated }) {
  const titleId = useId()
  const noteId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const createContact = useCreateAdminClientContact()
  const [errorMessage, setErrorMessage] = useState(null)
  const [showPassword, setShowPassword] = useState(false)
  const [showConfirmPassword, setShowConfirmPassword] = useState(false)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors },
  } = useForm({
    resolver: zodResolver(schema),
    defaultValues: {
      fullName: '',
      email: '',
      phone: '',
      password: '',
      confirmPassword: '',
    },
  })

  function onSubmit(values) {
    setErrorMessage(null)

    // The password is cleared the moment the request leaves. On success the
    // dialog closes anyway; on failure the Admin is retyping it deliberately,
    // which is the only moment it is safe to require.
    reset({ fullName: '', email: '', phone: '', password: '', confirmPassword: '' })

    createContact.mutate(
      {
        clientId,
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        // Omitted rather than sent as "": "" is a real value to the handler.
        phone: values.phone.trim() || undefined,
        password: values.password,
      },
      {
        // No toast on success carries the credential. The Admin typed it and
        // already has it, so there is nothing to display — see the file header.
        onSuccess: (created) => {
          onCreated(created)
        },
        onError: (error) =>
          handleSubmitError(
            error,
            setError,
            setErrorMessage,
            'Unable to create this contact. Please try again.',
          ),
      },
    )
  }

  return (
    <div className="fixed inset-0 z-50 overflow-y-auto">
      <div
        className="fixed inset-0 bg-slate-900/40"
        onClick={onClose}
        aria-hidden="true"
      />

      <div className="relative flex min-h-full items-end justify-center p-0 sm:items-center sm:p-6">
        <div
          ref={panelRef}
          role="dialog"
          aria-modal="true"
          aria-labelledby={titleId}
          tabIndex={-1}
          className="relative w-full max-w-lg rounded-t-[14px] border border-[#E2E4E9] bg-white p-5 shadow-[0_8px_24px_rgba(28,31,38,0.10)] focus:outline-none sm:rounded-[14px] sm:p-6"
        >
          <h2
            id={titleId}
            className="text-base font-semibold tracking-tight text-[#16181D]"
          >
            Add client contact
          </h2>

          <p className="mt-1 text-xs text-[#6B7280]">
            This creates a sign-in account for this company&apos;s client portal.
          </p>

          <div
            id={noteId}
            role="note"
            className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/70 px-3.5 py-3"
          >
            <Info
              size={15}
              strokeWidth={2}
              className="mt-0.5 shrink-0 text-[#6B7280]"
              aria-hidden="true"
            />
            <div className="min-w-0 text-xs text-[#6B7280]">
              <p>
                The password you enter will be shared with the client. It will not be
                shown again.
              </p>
              <p className="mt-1">
                The account is created awaiting approval and cannot sign in until you
                approve it. The email address does not need verifying — there is no
                verification step for this account.
              </p>
            </div>
          </div>

          {errorMessage && (
            <div
              role="alert"
              className="mt-4 flex items-center gap-2.5 rounded-[10px] border border-red-200 bg-red-50/60 px-4 py-2.5"
            >
              <AlertCircle
                size={14}
                strokeWidth={2}
                className="shrink-0 text-[#DC2626]"
                aria-hidden="true"
              />
              <p className="text-xs font-medium text-[#DC2626]">{errorMessage}</p>
            </div>
          )}

          <form
            onSubmit={handleSubmit(onSubmit)}
            noValidate
            aria-describedby={noteId}
            className="mt-5 flex flex-col gap-4"
          >
            <FormField label="Full name" htmlFor="adminContactFullName" error={errors.fullName?.message}>
              <input
                id="adminContactFullName"
                type="text"
                autoComplete="name"
                disabled={createContact.isPending}
                className={errors.fullName ? inputErrorClass : inputClass}
                {...register('fullName')}
              />
            </FormField>

            <FormField label="Email" htmlFor="adminContactEmail" error={errors.email?.message}>
              <input
                id="adminContactEmail"
                type="email"
                autoComplete="off"
                disabled={createContact.isPending}
                className={errors.email ? inputErrorClass : inputClass}
                {...register('email')}
              />
            </FormField>

            <FormField label="Phone" htmlFor="adminContactPhone" error={errors.phone?.message}>
              <input
                id="adminContactPhone"
                type="tel"
                autoComplete="tel"
                disabled={createContact.isPending}
                className={errors.phone ? inputErrorClass : inputClass}
                {...register('phone')}
              />
            </FormField>

            <FormField label="Password" htmlFor="adminContactPassword" error={errors.password?.message}>
              <div className="relative">
                <input
                  id="adminContactPassword"
                  type={showPassword ? 'text' : 'password'}
                  autoComplete="new-password"
                  disabled={createContact.isPending}
                  className={`${errors.password ? inputErrorClass : inputClass} pr-10`}
                  {...register('password')}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword(!showPassword)}
                  disabled={createContact.isPending}
                  aria-label={showPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showPassword}
                  className="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3.5 text-[#9CA3AF] transition-colors hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] disabled:cursor-not-allowed"
                >
                  {showPassword ? (
                    <EyeOff className="h-4 w-4" aria-hidden="true" />
                  ) : (
                    <Eye className="h-4 w-4" aria-hidden="true" />
                  )}
                </button>
              </div>
            </FormField>

            <FormField
              label="Confirm password"
              htmlFor="adminContactConfirmPassword"
              error={errors.confirmPassword?.message}
            >
              <div className="relative">
                <input
                  id="adminContactConfirmPassword"
                  type={showConfirmPassword ? 'text' : 'password'}
                  autoComplete="new-password"
                  disabled={createContact.isPending}
                  className={`${errors.confirmPassword ? inputErrorClass : inputClass} pr-10`}
                  {...register('confirmPassword')}
                />
                <button
                  type="button"
                  onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                  disabled={createContact.isPending}
                  aria-label={showConfirmPassword ? 'Hide password' : 'Show password'}
                  aria-pressed={showConfirmPassword}
                  className="absolute inset-y-0 right-0 flex cursor-pointer items-center pr-3.5 text-[#9CA3AF] transition-colors hover:text-[#16181D] focus:outline-none focus:text-[#0F9D74] disabled:cursor-not-allowed"
                >
                  {showConfirmPassword ? (
                    <EyeOff className="h-4 w-4" aria-hidden="true" />
                  ) : (
                    <Eye className="h-4 w-4" aria-hidden="true" />
                  )}
                </button>
              </div>
            </FormField>

            <p className="text-xs text-[#6B7280]">
              The email must be unique across every user account in the system, not
              just this company&apos;s contacts. The role is set to Client by the server
              and cannot be chosen here. The password is between 8 and 100 characters.
              Name, email, phone and password cannot be changed after this — the update
              route only activates and deactivates the account, so a mistyped password
              means deleting the contact and creating them again.
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
                // Disabled while pending: the create route generates a real account, so a
                // double submit would create two accounts rather than one plus a
                // rejected duplicate.
                disabled={createContact.isPending}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {createContact.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Creating…</span>
                  </>
                ) : (
                  <span>Create contact</span>
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
 * Shared failure handling.
 *
 * A 409 lands on the email field, because on this route it is always the email:
 * the handler raises it for an address that is already in use, and there is no
 * other uniqueness constraint here. The message names the real scope — every
 * user account, not this company's contacts — because "duplicate contact" would
 * send the Admin to the wrong list to look for the conflict.
 *
 * A 404 means the company is gone or soft-deleted; its contact routes are scoped
 * to a parent that is not deleted, so no contact can be added to one.
 */
function handleSubmitError(error, setError, setErrorMessage, fallback) {
  if (error?.response?.status === 409) {
    setError('email', {
      type: 'server',
      message: extractApiErrorMessage(
        error,
        'That email address already belongs to another account.',
      ),
    })
    return
  }

  if (error?.response?.status === 404) {
    const message = 'This company no longer exists, so no contact can be added to it.'
    setErrorMessage(message)
    toast.error(message)
    return
  }

  const fieldErrors = apiFieldErrors(error)
  const failedFields = Object.entries(fieldErrors)

  if (failedFields.length > 0) {
    failedFields.forEach(([field, message]) => {
      setError(field, { type: 'server', message })
    })
    return
  }

  const message = extractApiErrorMessage(error, fallback)
  setErrorMessage(message)
  toast.error(message)
}

export default AdminClientContactFormDialog
