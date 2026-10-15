import { useEffect, useId, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { AlertCircle, AlertTriangle, Eye, EyeOff, Loader2 } from 'lucide-react'
import { toast } from 'sonner'
import FormField, { inputClass, inputErrorClass } from '../../client/settings/FormField'
import { useFocusTrap } from '../../../hooks/useFocusTrap'
import {
  useCreateAdminStaff,
  useUpdateAdminStaff,
} from '../../../hooks/admin/useAdminStaffMutations'
import { apiFieldErrors } from '../../../utils/apiFieldErrors'
import { extractApiErrorMessage } from '../../../utils/apiError'
import { staffName } from './staffDisplay'

/**
 * Create and edit dialog for a staff account — one component, two modes, because
 * the two forms are the same four inputs with different visibility rules.
 *
 * THE PAYLOADS DIFFER BECAUSE THE DTOs DIFFER, AND THE DIFFERENCE IS LOAD-BEARING:
 *
 *   create -> CreateStaffRequestDto  { fullName, email, password, phone? }
 *   update -> UpdateStaffRequestDto  { fullName, email, phone?, isApproved }
 *
 * So create has a password and no isApproved; update has an isApproved and no
 * password. Neither carries a role: the handlers own it, and no endpoint accepts
 * one, so no role control appears in either mode.
 *
 * WHY isApproved IS SEEDED, NOT DEFAULTED. The update is a full replacement with
 * a PATCH verb, and UpdateStaffCommandHandler writes every field it is given. A
 * checkbox left at its default `false` would therefore silently un-approve an
 * approved account during an edit that only touched the phone number. The value
 * is read from the record being edited on every open, so an unrelated save
 * cannot change it.
 *
 * WHY fullName AND email ARE REQUIRED EVEN THOUGH THE BACKEND HAS NO VALIDATOR.
 * UpdateStaffCommandHandler calls request.FullName.Trim() and
 * request.Email.Trim() with no null guard, so omitting either is a 500
 * NullReferenceException rather than a validation error. The two required
 * refinements below exist to stop that, not to add a rule the server does not
 * have. No length, format or uniqueness rule is invented — the server checks
 * email uniqueness itself and reports it on the field.
 *
 * NO PASSWORD RULES ARE INVENTED. The backend's only password check on this
 * route is a non-empty string, so the schema checks exactly that and the UI says
 * so. A strength meter or complexity checklist here would be the frontend
 * asserting a policy the server does not have.
 *
 * THE CREATE NOTICE IS SHOWN BEFORE SUBMIT, and it warns about the password
 * rather than about the account's status, because the password is the part that
 * genuinely cannot be recovered. The create handler sets IsActive, IsApproved and
 * EmailVerified all to true, so the new Agent can sign in immediately; what it
 * does not do is ever return the password, so once this dialog closes the Admin
 * has no way to read it back and no way to reset it. The notice says that. It
 * used to claim the account could not sign in because of EmailVerified = false,
 * which the handler no longer does.
 */
const baseFields = {
  fullName: z.string().min(1, 'Full name is required.'),
  email: z.string().min(1, 'Email is required.'),
  phone: z.string(),
}

const createSchema = z
  .object({
    ...baseFields,
    password: z.string().min(1, 'Password is required.'),
  })
  // Trim for the emptiness test only, so a valid value keeps its internal spaces.
  .refine((values) => values.fullName.trim().length > 0, {
    message: 'Full name cannot be only whitespace.',
    path: ['fullName'],
  })
  .refine((values) => values.email.trim().length > 0, {
    message: 'Email cannot be only whitespace.',
    path: ['email'],
  })
  .refine((values) => values.password.trim().length > 0, {
    message: 'Password is required.',
    path: ['password'],
  })

const updateSchema = z
  .object({
    ...baseFields,
    // Seeded from the record, never defaulted — see the file header.
    isApproved: z.boolean(),
  })
  .refine((values) => values.fullName.trim().length > 0, {
    message: 'Full name cannot be only whitespace.',
    path: ['fullName'],
  })
  .refine((values) => values.email.trim().length > 0, {
    message: 'Email cannot be only whitespace.',
    path: ['email'],
  })

function AdminStaffFormDialog({ mode, staff, onClose, onSaved }) {
  const isEdit = mode === 'edit'
  const titleId = useId()
  const warningId = useId()
  const panelRef = useFocusTrap({ isOpen: true, onClose })

  const createStaff = useCreateAdminStaff()
  const updateStaff = useUpdateAdminStaff()
  const mutation = isEdit ? updateStaff : createStaff

  const [showPassword, setShowPassword] = useState(false)
  const [errorMessage, setErrorMessage] = useState(null)

  const {
    register,
    handleSubmit,
    reset,
    setError,
    formState: { errors, isDirty },
  } = useForm({
    resolver: zodResolver(isEdit ? updateSchema : createSchema),
    defaultValues: isEdit
      ? {
          fullName: staff?.fullName ?? '',
          email: staff?.email ?? '',
          phone: staff?.phone ?? '',
          // Read from the record, not defaulted. This is the field that a
          // partial payload would silently zero.
          isApproved: Boolean(staff?.isApproved),
        }
      : { fullName: '', email: '', password: '', phone: '' },
  })

  // Re-seed when a different record is edited into the same mounted dialog, so a
  // stale isApproved from the previous row cannot be submitted against this one.
  useEffect(() => {
    if (!isEdit) return
    reset({
      fullName: staff?.fullName ?? '',
      email: staff?.email ?? '',
      phone: staff?.phone ?? '',
      isApproved: Boolean(staff?.isApproved),
    })
  }, [isEdit, staff, reset])

  function onSubmit(values) {
    setErrorMessage(null)

    if (isEdit) {
      // All four DTO fields, always. `phone` is sent as null when cleared because
      // that is how the field is emptied — omitting it would rely on a
      // partial-patch semantic this endpoint does not have.
      updateStaff.mutate(
        {
          staffId: staff?.id,
          fullName: values.fullName.trim(),
          email: values.email.trim(),
          phone: values.phone.trim() ? values.phone.trim() : null,
          isApproved: values.isApproved,
        },
        {
          onSuccess: () => {
            toast.success(`${values.fullName.trim()} updated.`)
            onSaved?.()
            onClose()
          },
          onError: (error) =>
            handleSubmitError(
              error,
              setError,
              setErrorMessage,
              'Unable to update this account. Please try again.',
            ),
        },
      )
      return
    }

    createStaff.mutate(
      {
        fullName: values.fullName.trim(),
        email: values.email.trim(),
        password: values.password,
        phone: values.phone.trim() ? values.phone.trim() : undefined,
      },
      {
        onSuccess: () => {
          // States the two things that are true and neither more: the account is
          // ready to use, and the password is gone for good.
          toast.success(
            `${values.fullName.trim()} created. They can sign in now — pass on the password you set, as it cannot be shown again.`,
          )
          onSaved?.()
          onClose()
        },
        onError: (error) =>
          handleSubmitError(
            error,
            setError,
            setErrorMessage,
            'Unable to create this account. Please try again.',
          ),
      },
    )
  }

  const dialogHeading = isEdit
    ? `Edit ${staffName(staff)}`
    : 'Create staff account'

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
            {dialogHeading}
          </h2>

          <p className="mt-1 text-xs text-[#6B7280]">
            {isEdit
              ? 'This update replaces the stored record in full. Every field below is sent on save.'
              : 'The role is set to Agent by the server and cannot be chosen here.'}
          </p>

          {!isEdit && (
            <div
              id={warningId}
              role="note"
              className="mt-4 flex items-start gap-2.5 rounded-[10px] border border-amber-200 bg-amber-50/60 px-3.5 py-3"
            >
              <AlertTriangle
                size={15}
                strokeWidth={2}
                className="mt-0.5 shrink-0 text-[#D97706]"
                aria-hidden="true"
              />
              <div className="min-w-0 text-xs text-[#92400E]">
                <p className="font-semibold">This account can sign in as soon as you create it.</p>
                <p className="mt-1">
                  No approval or email verification is needed. The password is the one
                  set here and the server never returns it, so share it with them
                  directly and keep a copy — it cannot be viewed or reset from Admin
                  afterwards.
                </p>
              </div>
            </div>
          )}

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
            aria-describedby={isEdit ? undefined : warningId}
            className="mt-5 flex flex-col gap-4"
          >
            <FormField label="Full name" htmlFor="adminStaffFullName" error={errors.fullName?.message}>
              <input
                id="adminStaffFullName"
                type="text"
                autoComplete="name"
                disabled={mutation.isPending}
                className={errors.fullName ? inputErrorClass : inputClass}
                {...register('fullName')}
              />
            </FormField>

            <FormField label="Email" htmlFor="adminStaffEmail" error={errors.email?.message}>
              <input
                id="adminStaffEmail"
                type="email"
                autoComplete="email"
                disabled={mutation.isPending}
                className={errors.email ? inputErrorClass : inputClass}
                {...register('email')}
              />
            </FormField>

            {isEdit ? (
              <fieldset className="rounded-[10px] border border-[#E2E4E9] bg-[#F7F8FA]/60 px-3.5 py-3">
                <legend className="px-1 text-xs font-medium text-[#6B7280]">Approval</legend>
                <div className="mt-1 flex items-start gap-2.5">
                  <input
                    id="adminStaffIsApproved"
                    type="checkbox"
                    disabled={mutation.isPending}
                    className="mt-0.5 h-4 w-4 shrink-0 cursor-pointer rounded-[4px] border-[#E2E4E9] text-[#0F9D74] focus:outline-none focus:ring-2 focus:ring-[rgba(15,157,116,0.15)]"
                    {...register('isApproved')}
                  />
                  <label
                    htmlFor="adminStaffIsApproved"
                    className="cursor-pointer text-xs text-[#16181D]"
                  >
                    Approved — this account is allowed to sign in
                  </label>
                </div>
                {errors.isApproved?.message && (
                  <p role="alert" className="mt-1.5 text-xs font-medium text-[#DC2626]">
                    {errors.isApproved.message}
                  </p>
                )}
              </fieldset>
            ) : (
              <FormField label="Password" htmlFor="adminStaffPassword" error={errors.password?.message}>
                <div className="relative">
                  <input
                    id="adminStaffPassword"
                    type={showPassword ? 'text' : 'password'}
                    autoComplete="new-password"
                    disabled={mutation.isPending}
                    className={`${errors.password ? inputErrorClass : inputClass} pr-10`}
                    placeholder="••••••••"
                    {...register('password')}
                  />
                  <button
                    type="button"
                    onClick={() => setShowPassword(!showPassword)}
                    disabled={mutation.isPending}
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
            )}

            <FormField label="Phone" htmlFor="adminStaffPhone" error={errors.phone?.message}>
              <input
                id="adminStaffPhone"
                type="tel"
                autoComplete="tel"
                disabled={mutation.isPending}
                className={errors.phone ? inputErrorClass : inputClass}
                {...register('phone')}
              />
            </FormField>

            {!isEdit && (
              <p className="text-xs text-[#6B7280]">
                The password is stored by the server and is never shown again. The
                server applies no complexity rules beyond it being non-empty.
              </p>
            )}

            <div className="flex flex-col-reverse gap-2 pt-1 sm:flex-row sm:justify-end">
              <button
                type="button"
                onClick={onClose}
                disabled={mutation.isPending}
                className="inline-flex cursor-pointer items-center justify-center rounded-[10px] border border-[#E2E4E9] bg-white px-4 py-2.5 text-sm font-semibold text-[#16181D] transition duration-150 hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-[#0F9D74] disabled:cursor-not-allowed disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="submit"
                disabled={mutation.isPending || (isEdit && !isDirty)}
                className="inline-flex cursor-pointer items-center justify-center gap-2 rounded-[10px] bg-[#1C1F26] px-4 py-2.5 text-sm font-semibold text-white shadow-[0_1px_3px_rgba(28,31,38,0.06)] transition duration-150 hover:bg-[#101319] focus:outline-none focus:ring-2 focus:ring-[#0F9D74] focus:ring-offset-2 disabled:cursor-not-allowed disabled:opacity-50"
              >
                {mutation.isPending ? (
                  <>
                    <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
                    <span>Saving…</span>
                  </>
                ) : (
                  <span>{isEdit ? 'Save changes' : 'Create account'}</span>
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
 * Shared failure handling for both modes.
 *
 * A 409 means the email is already taken — the handlers raise
 * InvalidOperationException for it — so that message belongs on the email field
 * rather than in a banner. Anything carrying field-level details is mapped onto
 * the matching inputs by name, and only a failure with no field to attach it to
 * reaches the banner.
 *
 * `fallback` is the last-resort sentence for a failure with no usable message of
 * its own; a 404 is called out separately because it means something specific
 * here — the id is no longer an Agent, or no longer exists — rather than a
 * generic transport problem.
 */
function handleSubmitError(error, setError, setErrorMessage, fallback) {
  if (error?.response?.status === 409) {
    setError('email', {
      type: 'server',
      message: extractApiErrorMessage(error, 'That email address is already in use.'),
    })
    return
  }

  if (error?.response?.status === 404) {
    const message = 'This staff record no longer exists, or is no longer an Agent.'
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

export default AdminStaffFormDialog
